using System.Security.Claims;
using attendanceCollector.Application.Interfaces;

namespace attendanceCollector.API.Services;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public string? Subject => Principal?.FindFirst("sub")?.Value;
    public string? Email => Principal?.FindFirst("email")?.Value;
    public bool IsBootstrapAdmin => Principal?.IsInRole("BootstrapAdmin") == true;
}
