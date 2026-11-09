using attendanceCollector.API.Extensions;
using attendanceCollector.Application.Services.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace attendanceCollector.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class UsersController(GetCurrentUserQueryHandler getCurrentUser) : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetCurrentUser() => Ok(getCurrentUser.Handle(new GetCurrentUserQuery()));

    [Authorize(
        Policy = "BootstrapAdmin",
        AuthenticationSchemes = AuthenticationExtensions.CookieScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("bootstrap-admin")]
    public IActionResult GetBootstrapAdminStatus() => Ok(new
    {
        status = "BootstrapAdmin",
        user = getCurrentUser.Handle(new GetCurrentUserQuery())
    });
}

