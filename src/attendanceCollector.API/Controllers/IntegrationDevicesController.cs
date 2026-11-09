using attendanceCollector.API.Extensions;
using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace attendanceCollector.API.Controllers;

[ApiController]
[Route("api/v1/devices")]
[Authorize(AuthenticationSchemes = CollectorIntegrationConstant.Scheme, Policy = CollectorIntegrationConstant.Policy)]
public sealed class IntegrationDevicesController(
    IAttendanceStore devices,
    IIntegrationStore integrations,
    IIntegrationRequestContext requestContext,
    IAttendanceCollectionService collections,
    IEnumerable<IAttendanceDeviceDriver> drivers,
    TimeProvider clock) : ControllerBase
{
    private readonly IReadOnlyDictionary<string, IAttendanceDeviceDriver> _drivers = drivers
        .ToDictionary(x => x.DriverCode, StringComparer.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var identity = GetIdentity();
        var all = await devices.GetDevicesAsync(cancellationToken);
        var visible = new List<IntegrationDeviceDto>();
        foreach (var device in all.Where(x => x.IsActive).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (await integrations.HasAccessAsync(identity, device.Id, clock.GetUtcNow().UtcDateTime, cancellationToken))
                visible.Add(new(device.Id, device.Code, device.Name, device.DriverCode, device.IsActive));
        }
        return Ok(visible);
    }

    [HttpPost("{deviceId:guid}/collect")]
    public async Task<IActionResult> Collect(Guid deviceId, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(deviceId, collection: true, cancellationToken);
        return Ok(await collections.CollectAsync(deviceId, cancellationToken));
    }

    [HttpPost("{deviceId:guid}/connection")]
    public async Task<IActionResult> CheckConnection(Guid deviceId, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(deviceId, collection: true, cancellationToken);
        var device = await devices.GetDeviceAsync(deviceId, cancellationToken);
        if (device is null || !device.IsActive) return NotFound();
        if (!_drivers.TryGetValue(device.DriverCode, out var driver))
            return Ok(new IntegrationConnectionDto(false, "Thiết bị không hỗ trợ kiểm tra kết nối."));
        try
        {
            await driver.CheckConnectionAsync(device, cancellationToken);
            return Ok(new IntegrationConnectionDto(true, null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Ok(new IntegrationConnectionDto(false, "Collector không kết nối được tới máy chấm công."));
        }
    }

    private IntegrationIdentity GetIdentity() => requestContext.Identity
        ?? throw new CollectorIntegrationException(401, CollectorIntegrationConstant.InvalidCredential, "Authentication failed.");

    private async Task EnsureAccessAsync(Guid deviceId, bool collection, CancellationToken cancellationToken)
    {
        var identity = GetIdentity();
        var permitted = collection
            ? await integrations.HasCollectionAccessAsync(identity, deviceId, clock.GetUtcNow().UtcDateTime, cancellationToken)
            : await integrations.HasAccessAsync(identity, deviceId, clock.GetUtcNow().UtcDateTime, cancellationToken);
        if (!permitted)
            throw new CollectorIntegrationException(403, CollectorIntegrationConstant.AccessDenied, "Access denied.");
    }
}

public sealed record IntegrationDeviceDto(Guid Id, string Code, string Name, string DriverCode, bool IsActive);
public sealed record IntegrationConnectionDto(bool IsConnected, string? ErrorMessage);
