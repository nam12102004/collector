using Microsoft.AspNetCore.Mvc;

namespace attendanceCollector.API.Services;

public interface IAuthenticationFlowService
{
    ChallengeResult CreateLoginChallenge(string? returnUrl);
    AntiforgeryTokenResponse CreateAntiforgeryToken(HttpContext context);
    Task<SignOutResult> CreateLogoutResultAsync(HttpContext context);
}

public sealed record AntiforgeryTokenResponse(string? RequestToken);
