using attendanceCollector.API.Extensions;
using attendanceCollector.API.Services;
using attendanceCollector.Application.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace attendanceCollector.API.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    IAuthenticationFlowService authenticationFlow,
    GetCurrentUserQueryHandler getCurrentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null) =>
        authenticationFlow.CreateLoginChallenge(returnUrl);

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.CookieScheme)]
    [HttpGet("me")]
    public IActionResult Me() => Ok(getCurrentUser.Handle(new GetCurrentUserQuery()));

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.CookieScheme)]
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken() => Ok(authenticationFlow.CreateAntiforgeryToken(HttpContext));

    [Authorize(AuthenticationSchemes = AuthenticationExtensions.CookieScheme)]
    [ValidateAntiForgeryToken]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout() =>
        await authenticationFlow.CreateLogoutResultAsync(HttpContext);
}

