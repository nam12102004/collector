using attendanceCollector.API.Extensions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Application.Contracts.Attendance;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace attendanceCollector.API.Controllers;

[ApiController]
[Route("api/v1/attendance")]
[Authorize(
    Policy = "BootstrapAdmin",
    AuthenticationSchemes = AuthenticationExtensions.CookieScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
public sealed class AttendanceController(IAttendanceCollectionService collections) : ControllerBase
{
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] AttendanceLogQuery query,
        CancellationToken cancellationToken) =>
        Ok(await collections.GetLogsAsync(query, cancellationToken));

    [HttpGet("collections")]
    public async Task<IActionResult> GetCollectionRuns(
        [FromQuery] Guid? sourceId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await collections.GetRunsAsync(sourceId, page, pageSize, cancellationToken));
}
