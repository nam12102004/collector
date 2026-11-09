using attendanceCollector.API.Extensions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace attendanceCollector.API.Services;

public sealed class AuthenticationFlowService(
    IConfiguration configuration,
    IAntiforgery antiforgery) : IAuthenticationFlowService
{
    public ChallengeResult CreateLoginChallenge(string? returnUrl)
    {
        var redirectUri = IsLocalUrl(returnUrl) ? returnUrl! : "/auth/me";
        var properties = new AuthenticationProperties { RedirectUri = redirectUri };
        return new ChallengeResult(AuthenticationExtensions.OpenIdConnectScheme, properties);
    }

    public AntiforgeryTokenResponse CreateAntiforgeryToken(HttpContext context)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return new AntiforgeryTokenResponse(tokens.RequestToken);
    }

    public async Task<SignOutResult> CreateLogoutResultAsync(HttpContext context)
    {
        var session = await context.AuthenticateAsync(AuthenticationExtensions.CookieScheme);
        var properties = new AuthenticationProperties
        {
            RedirectUri = configuration["OIDC_POST_LOGOUT_REDIRECT_URI"]
                ?? configuration["Oidc:PostLogoutRedirectUri"]
        };
        var idToken = session.Properties?.GetTokenValue("id_token");
        if (!string.IsNullOrWhiteSpace(idToken))
        {
            properties.StoreTokens(
            [
                new AuthenticationToken { Name = "id_token", Value = idToken }
            ]);
        }

        return new SignOutResult(
            [CookieAuthenticationDefaults.AuthenticationScheme, AuthenticationExtensions.OpenIdConnectScheme],
            properties);
    }

    private static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (url[0] == '/')
        {
            return (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))
                && !url.Contains('\\');
        }

        return url[0] == '~' && url.Length > 1 && url[1] == '/';
    }
}
