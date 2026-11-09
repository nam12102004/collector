using attendanceCollector.API.Extensions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Application.Contracts.Attendance;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace attendanceCollector.API.Controllers;

[ApiController]
[Route("api/v1/attendance-sources")]
[Authorize(
    Policy = "BootstrapAdmin",
    AuthenticationSchemes = AuthenticationExtensions.CookieScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
public sealed class AttendanceSourcesController(
    IAttendanceSourceService sources,
    IAttendanceCollectionService collections) : ControllerBase
{
    [HttpGet("drivers")]
    public IActionResult GetSupportedDrivers() => Ok(sources.GetSupportedDrivers());

    [HttpGet]
    public async Task<IActionResult> GetSources(CancellationToken cancellationToken) =>
        Ok(await sources.GetSourcesAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSource(
        [FromBody] AttendanceSourceRequest request,
        CancellationToken cancellationToken)
    {
        var source = await sources.AddAsync(request, cancellationToken);
        return Created("/api/v1/attendance-sources", source);
    }

    [HttpPut("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSource(
        Guid id,
        [FromBody] AttendanceSourceRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sources.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(
        Guid id,
        [FromBody] SetAttendanceSourceActiveRequest request,
        CancellationToken cancellationToken)
    {
        await sources.SetActiveAsync(id, request.IsActive, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/collect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Collect(Guid id, CancellationToken cancellationToken) =>
        Ok(await collections.CollectAsync(id, cancellationToken));
}

public sealed record SetAttendanceSourceActiveRequest(bool IsActive);
