using attendanceCollector.API.Extensions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.MockDevice.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace attendanceCollector.API.Controllers;

[ApiController]
[Route("api/v1/attendance-ingestion")]
[Authorize(AuthenticationSchemes = MockDeviceApiKeyAuthenticationHandler.SchemeName)]
public sealed class AttendanceIngestionController(IAttendanceIngestionService ingestion) : ControllerBase
{
    [HttpPost("mock-device")]
    public async Task<IActionResult> ReceiveMockDeviceBatch(
        [FromBody] MockDeviceAttendanceBatchV1 batch,
        CancellationToken cancellationToken) =>
        Ok(await ingestion.ReceiveMockDeviceBatchAsync(batch, cancellationToken));
}
