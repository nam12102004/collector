using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace attendanceCollector.API.Extensions;

public static class AuthenticationExtensions
{
    public const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string OpenIdConnectScheme = OpenIdConnectDefaults.AuthenticationScheme;

    public static AuthenticationBuilder AddKeycloakAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var issuer = Required(configuration, "OIDC_ISSUER", "Oidc:Issuer");
        var metadataAddress = configuration["OIDC_METADATA_ADDRESS"]
            ?? configuration["Oidc:MetadataAddress"]
            ?? $"{issuer.TrimEnd('/')}/.well-known/openid-configuration";
        var audience = Required(configuration, "OIDC_AUDIENCE", "Oidc:Audience");
        var clientId = Required(configuration, "OIDC_CLIENT_ID", "Oidc:ClientId");
        var redirectUri = Required(configuration, "OIDC_REDIRECT_URI", "Oidc:RedirectUri");
        var postLogoutRedirectUri = Required(configuration, "OIDC_POST_LOGOUT_REDIRECT_URI", "Oidc:PostLogoutRedirectUri");
        var scopes = configuration["OIDC_SCOPES"] ?? configuration["Oidc:Scopes"] ?? "openid profile email";
        var validateAudience = configuration.GetValue("OIDC_VALIDATE_AUDIENCE",
            configuration.GetValue("Oidc:ValidateAudience", true));
        var requireHttpsMetadata = configuration.GetValue("OIDC_REQUIRE_HTTPS_METADATA",
            configuration.GetValue("Oidc:RequireHttpsMetadata", true));

        var issuerUri = ParseAbsoluteHttpUri(issuer, "OIDC_ISSUER");
        var metadataUri = ParseAbsoluteHttpUri(metadataAddress, "OIDC_METADATA_ADDRESS");
        var redirect = ParseAbsoluteHttpUri(redirectUri, "OIDC_REDIRECT_URI");
        var postLogoutRedirect = ParseAbsoluteHttpUri(postLogoutRedirectUri, "OIDC_POST_LOGOUT_REDIRECT_URI");
        if (!environment.IsDevelopment()
            && new[] { issuerUri, metadataUri, redirect, postLogoutRedirect }
                .Any(uri => uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Keycloak issuer, metadata, callback, and logout URLs must use HTTPS outside Development.");
        }

        var callbackPath = new PathString(redirect.AbsolutePath);
        var signOutCallbackUri = new Uri(redirect.GetLeftPart(UriPartial.Authority) + "/signout-callback-oidc");
        var cookieSecurePolicy = environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "attendanceCollector.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = cookieSecurePolicy;
        });

        var authentication = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

        authentication.AddJwtBearer(options =>
        {
            options.Authority = issuer;
            options.MetadataAddress = metadataAddress;
            options.BackchannelHttpHandler = CreateBackchannelHttpHandler(issuerUri, metadataUri);
            options.Audience = validateAudience ? audience : null;
            options.RequireHttpsMetadata = requireHttpsMetadata;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = validateAudience,
                ValidAudience = validateAudience ? audience : null
            };
        });

        authentication.AddCookie(CookieScheme, options =>
        {
            options.Cookie.Name = "attendanceCollector.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = cookieSecurePolicy;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        authentication.AddOpenIdConnect(OpenIdConnectScheme, options =>
        {
            options.Authority = issuer;
            options.MetadataAddress = metadataAddress;
            options.BackchannelHttpHandler = CreateBackchannelHttpHandler(issuerUri, metadataUri);
            options.ClientId = clientId;
            var clientSecret = configuration["OIDC_CLIENT_SECRET"] ?? configuration["Oidc:ClientSecret"];
            options.ClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret;
            options.SignInScheme = CookieScheme;
            options.CallbackPath = callbackPath;
            options.SignedOutCallbackPath = "/signout-callback-oidc";
            options.SignedOutRedirectUri = postLogoutRedirectUri;
            options.RequireHttpsMetadata = requireHttpsMetadata;
            options.ResponseType = "code";
            options.UsePkce = true;
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = clientId,
                NameClaimType = "name",
                RoleClaimType = ClaimTypes.Role
            };
            options.Scope.Clear();
            foreach (var scope in scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                options.Scope.Add(scope);
            }

            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = cookieSecurePolicy;
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            options.NonceCookie.SecurePolicy = cookieSecurePolicy;
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.RedirectUri = redirectUri;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToIdentityProviderForSignOut = context =>
            {
                context.ProtocolMessage.PostLogoutRedirectUri = signOutCallbackUri.ToString();
                return Task.CompletedTask;
            };
        });

        return authentication;
    }

    private static string Required(IConfiguration configuration, string environmentKey, string sectionKey) =>
        configuration[environmentKey]
        ?? configuration[sectionKey]
        ?? throw new InvalidOperationException($"{environmentKey} must be configured.");

    private static Uri ParseAbsoluteHttpUri(string value, string key)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{key} must be an absolute HTTP or HTTPS URL.");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"{key} cannot include a query string or fragment.");
        }

        return uri;
    }

    private static HttpMessageHandler CreateBackchannelHttpHandler(Uri issuerUri, Uri metadataUri) =>
        new OidcBackchannelHostRewriteHandler(issuerUri, metadataUri)
        {
            InnerHandler = new HttpClientHandler()
        };

    private sealed class OidcBackchannelHostRewriteHandler(Uri issuerUri, Uri metadataUri) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri is { } requestUri
                && requestUri.Scheme.Equals(issuerUri.Scheme, StringComparison.OrdinalIgnoreCase)
                && requestUri.Host.Equals(issuerUri.Host, StringComparison.OrdinalIgnoreCase)
                && requestUri.Port == issuerUri.Port)
            {
                request.RequestUri = new UriBuilder(requestUri)
                {
                    Scheme = metadataUri.Scheme,
                    Host = metadataUri.Host,
                    Port = metadataUri.Port
                }.Uri;
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
