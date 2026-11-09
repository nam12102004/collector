using System.Security.Claims;
using System.Text.Encodings.Web;
using attendanceCollector.API.Helpers;
using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace attendanceCollector.API.Extensions;

public sealed class IntegrationApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IIntegrationCredentialService credentials)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var values) || values.Count != 1 || values[0] is not { } value)
            return AuthenticateResult.Fail("Authentication failed.");
        var identity = await credentials.AuthenticateAsync(value, Context.RequestAborted);
        if (identity is null) return AuthenticateResult.Fail("Authentication failed.");
        var claims = new ClaimsIdentity([
            new Claim(CollectorIntegrationConstant.ClientClaim, identity.ClientId.ToString("D")),
            new Claim(CollectorIntegrationConstant.KeyClaim, identity.KeyId.ToString("D"))], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(claims), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        IntegrationProblemHelper.WriteAsync(Context, 401, CollectorIntegrationConstant.InvalidCredential, "Authentication failed.", Context.RequestAborted);

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        IntegrationProblemHelper.WriteAsync(Context, 403, CollectorIntegrationConstant.AccessDenied, "Access denied.", Context.RequestAborted);
}
