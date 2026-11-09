using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace attendanceCollector.API.Controllers;

[ApiController]
[Route("api/v1/devices/{deviceId}/attendance-logs")]
[Authorize(AuthenticationSchemes = CollectorIntegrationConstant.Scheme, Policy = CollectorIntegrationConstant.Policy)]
[EnableRateLimiting(CollectorIntegrationConstant.Policy)]
[IntegrationRowCount]
public sealed class DeviceAttendanceLogsController(ICollectorAttendanceReadService service) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    public async Task<IActionResult> Read(string deviceId, [FromQuery] CollectorLogRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ReadAsync(deviceId, request, cancellationToken));
}

public sealed class IntegrationRowCountAttribute : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: CollectorLogPage page })
            context.HttpContext.Items["IntegrationRowCount"] = page.Items.Count;
    }
}
