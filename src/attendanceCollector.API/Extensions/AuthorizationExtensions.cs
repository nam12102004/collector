using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace attendanceCollector.API.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddApiAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var email = configuration["BOOTSTRAP_ADMIN_EMAIL"];
        var externalSubject = configuration["BOOTSTRAP_ADMIN_EXTERNAL_SUBJECT"];
        var enabledValue = configuration["BOOTSTRAP_ENABLED"];
        var bootstrapEnabled = string.IsNullOrWhiteSpace(enabledValue)
            ? !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(externalSubject)
            : bool.TryParse(enabledValue, out var parsedEnabled)
                ? parsedEnabled
                : throw new InvalidOperationException("BOOTSTRAP_ENABLED must be true, false, or empty.");

        if (bootstrapEnabled && (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(externalSubject)))
        {
            throw new InvalidOperationException(
                "BOOTSTRAP_ADMIN_EMAIL and BOOTSTRAP_ADMIN_EXTERNAL_SUBJECT are required when bootstrap is enabled.");
        }

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy("BootstrapAdmin", policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => bootstrapEnabled
                    && context.User.IsInRole("BootstrapAdmin")
                    && string.Equals(context.User.FindFirst("sub")?.Value, externalSubject, StringComparison.Ordinal)
                    && string.Equals(context.User.FindFirst("email")?.Value, email, StringComparison.OrdinalIgnoreCase)));

        if (bootstrapEnabled)
        {
            services.AddTransient<IClaimsTransformation>(_ =>
                new BootstrapAdminClaimsTransformation(email!, externalSubject!));
        }

        return services;
    }
}


