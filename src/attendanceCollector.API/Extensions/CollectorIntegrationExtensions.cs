using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using attendanceCollector.API.Helpers;
using attendanceCollector.API.Services;
using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace attendanceCollector.API.Extensions;

public static class CollectorIntegrationExtensions
{
    public static IServiceCollection AddCollectorIntegration(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var section = configuration.GetSection("CollectorIntegration");
        services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<IConfiguration>().GetSection("CollectorIntegration").Get<CollectorIntegrationOptions>() ?? new();
            if (settings.SnapshotLifetimeMinutes is < 1 or > 60 || settings.MaximumSnapshotRows is < 1 or > 1_000_000
                || settings.MaximumSnapshotBytes is < 1024 or > 256 * 1024 * 1024 || settings.MaximumResponseBytes is < 1024 or > 5 * 1024 * 1024
                || settings.MaximumSnapshotsPerClient is < 1 or > 100 || settings.MaximumSnapshotsTotal is < 1 or > 1000
                || settings.RequestsPerMinute is < 1 or > 1000 || settings.ConcurrentRequestsPerClient is < 1 or > 16
                || settings.ConcurrentRequestsTotal is < 1 or > 128 || settings.RequestTimeoutSeconds is < 1 or > 60
                || settings.DatabaseTimeoutSeconds < 1 || settings.DatabaseTimeoutSeconds > settings.RequestTimeoutSeconds)
                throw new InvalidOperationException("CollectorIntegration resource limits are invalid.");
            return settings;
        });
        services.AddScoped<IIntegrationRequestContext, HttpIntegrationRequestContext>();
        services.AddScoped<ICollectorAttendanceReadService, CollectorAttendanceReadService>();
        services.AddScoped<IIntegrationClientService, IntegrationClientService>();
        services.AddSingleton<CollectorIntegrationMetricsService>();
        services.AddSingleton<ICollectorPageTokenService>(provider =>
        {
            var settings = provider.GetRequiredService<CollectorIntegrationOptions>();
            if (!settings.Enabled) return new CollectorPageTokenService(provider.GetRequiredService<IDataProtectionProvider>());
            var keySection = provider.GetRequiredService<IConfiguration>().GetSection("CollectorIntegration");
            var host = provider.GetRequiredService<IWebHostEnvironment>();
            var path = keySection["KeyRingPath"];
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new InvalidOperationException("CollectorIntegration:KeyRingPath must be an absolute persistent directory.");
            var certificatePath = keySection["KeyEncryptionCertificatePath"];
            if (!host.IsDevelopment() && string.IsNullOrWhiteSpace(certificatePath))
                throw new InvalidOperationException("A key-encryption certificate is required outside Development.");
            var certificate = string.IsNullOrWhiteSpace(certificatePath) ? null
                : X509CertificateLoader.LoadPkcs12FromFile(certificatePath, keySection["KeyEncryptionCertificatePassword"]);
            if (certificate is not null && (!certificate.HasPrivateKey || certificate.GetRSAPublicKey() is null))
                throw new InvalidOperationException("The key-encryption certificate must contain an RSA private key.");
            var protection = DataProtectionProvider.Create(new DirectoryInfo(path), builder =>
            {
                builder.SetApplicationName("attendanceCollector.HrmPages");
                if (certificate is not null) builder.ProtectKeysWithCertificate(certificate);
            });
            // Verify persistence and encryption before readiness can report success.
            var probe = protection.CreateProtector("startup-check");
            _ = probe.Unprotect(probe.Protect("ready"));
            return new CollectorPageTokenService(protection);
        });
        services.AddHostedService<CollectorSnapshotCleanupService>();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var address in (section.GetSection("TrustedProxyAddresses").Get<string[]>() ?? []).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!IPAddress.TryParse(address, out var ip)) throw new InvalidOperationException("TrustedProxyAddresses must contain IP addresses.");
                options.KnownProxies.Add(ip);
            }
            // Empty KnownProxies/KnownIPNetworks otherwise means trust every sender.
            if (options.KnownProxies.Count == 0) options.ForwardedHeaders = ForwardedHeaders.None;
        });

        // Authenticate the route's explicit scheme before the rate limiter partitions by trusted client ID.
        services.AddAuthentication(options => options.DefaultAuthenticateScheme = "CollectorRouteAuthentication")
            .AddScheme<AuthenticationSchemeOptions, IntegrationApiKeyAuthenticationHandler>(CollectorIntegrationConstant.Scheme, _ => { })
            .AddPolicyScheme("CollectorRouteAuthentication", null, options => options.ForwardDefaultSelector = context =>
                IsRead(context) ? CollectorIntegrationConstant.Scheme : JwtBearerDefaults.AuthenticationScheme);
        services.AddAuthorizationBuilder().AddPolicy(CollectorIntegrationConstant.Policy, policy => policy
            .AddAuthenticationSchemes(CollectorIntegrationConstant.Scheme).RequireAuthenticatedUser().RequireClaim(CollectorIntegrationConstant.ClientClaim));

        services.Configure<ApiBehaviorOptions>(options =>
        {
            var previous = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = action =>
            {
                if (!IsRead(action.HttpContext) && !action.HttpContext.Request.Path.StartsWithSegments("/api/v1/integration-clients")) return previous(action);
                var result = new ObjectResult(IntegrationProblemHelper.Create(action.HttpContext, 400, CollectorIntegrationConstant.InvalidRequest, "Request values are invalid.")) { StatusCode = 400 };
                result.ContentTypes.Add("application/problem+json");
                return result;
            };
        });

        services.AddOptions<RateLimiterOptions>().Configure<CollectorIntegrationOptions>((options, settings) =>
        {
            options.AddPolicy(CollectorIntegrationConstant.Policy, context => RateLimitPartition.GetFixedWindowLimiter(Client(context), _ => new()
            { PermitLimit = settings.RequestsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context => !IsRead(context) ? RateLimitPartition.GetNoLimiter("other")
                    : RateLimitPartition.GetConcurrencyLimiter(Client(context), _ => new() { PermitLimit = settings.ConcurrentRequestsPerClient, QueueLimit = 0 })),
                PartitionedRateLimiter.Create<HttpContext, string>(context => !IsRead(context) ? RateLimitPartition.GetNoLimiter("other")
                    : RateLimitPartition.GetConcurrencyLimiter("all", _ => new() { PermitLimit = settings.ConcurrentRequestsTotal, QueueLimit = 0 })));
            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var quota = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry);
                var seconds = quota ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : 1;
                rejected.HttpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await IntegrationProblemHelper.WriteAsync(rejected.HttpContext, quota ? 429 : 503,
                    quota ? CollectorIntegrationConstant.RateLimited : CollectorIntegrationConstant.Capacity,
                    "Request capacity is busy. Try again later.", cancellationToken);
            };
        });
        return services;
    }

    private static bool IsRead(HttpContext context) => context.Request.Path.StartsWithSegments("/api/v1/devices");
    private static string Client(HttpContext context) => context.User.FindFirst(CollectorIntegrationConstant.ClientClaim)?.Value ?? "anonymous";
}
