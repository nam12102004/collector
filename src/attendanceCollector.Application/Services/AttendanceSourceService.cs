using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Contracts.Attendance;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using attendanceCollector.MockDevice.Contracts;

namespace attendanceCollector.Application.Services;

public sealed class AttendanceSourceService(
    IAttendanceStore store,
    IEnumerable<IAttendanceDeviceDriver> drivers,
    TimeProvider clock) : IAttendanceSourceService
{
    private readonly IReadOnlyDictionary<string, IAttendanceDeviceDriver> _drivers = drivers
        .ToDictionary(driver => driver.DriverCode, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AttendanceDriverDto> GetSupportedDrivers() => _drivers.Values
        .Where(driver => !string.Equals(driver.DriverCode, MockDeviceContractConstant.DriverCode, StringComparison.Ordinal))
        .OrderBy(driver => driver.DisplayName, StringComparer.OrdinalIgnoreCase)
        .Select(driver => new AttendanceDriverDto(driver.DriverCode, driver.DisplayName, driver.Description))
        .ToArray();

    public async Task<IReadOnlyList<AttendanceSourceDto>> GetSourcesAsync(CancellationToken cancellationToken)
    {
        var devices = await store.GetDevicesAsync(cancellationToken);
        return devices
            .Where(device => _drivers.ContainsKey(device.DriverCode))
            .OrderBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
            .Select(Map)
            .ToArray();
    }

    public async Task<AttendanceSourceDto> AddAsync(
        AttendanceSourceRequest request,
        CancellationToken cancellationToken)
    {
        var driverCode = ResolveDriverCode(request.DriverCode);
        Validate(request);
        var code = NormalizeCode(request.Code);
        if (await store.DeviceCodeExistsAsync(code, null, cancellationToken))
        {
            throw new AttendanceSourceConflictException(code);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var device = AttendanceDevice.Create(
            code,
            request.Name,
            driverCode,
            request.Host,
            request.Port,
            request.CommunicationKey,
            request.TimeoutSeconds,
            request.TimeZoneId,
            now);
        await store.AddDeviceAsync(device, cancellationToken);
        return Map(device);
    }

    public async Task<AttendanceSourceDto> UpdateAsync(
        Guid id,
        AttendanceSourceRequest request,
        CancellationToken cancellationToken)
    {
        var device = await store.GetDeviceAsync(id, cancellationToken)
            ?? throw new AttendanceSourceNotFoundException(id);
        var driverCode = ResolveDriverCode(request.DriverCode);
        Validate(request);
        var code = NormalizeCode(request.Code);
        if (await store.DeviceCodeExistsAsync(code, id, cancellationToken))
        {
            throw new AttendanceSourceConflictException(code);
        }

        device.Update(
            code,
            request.Name,
            driverCode,
            request.Host,
            request.Port,
            request.CommunicationKey,
            request.TimeoutSeconds,
            request.TimeZoneId,
            clock.GetUtcNow().UtcDateTime);
        await store.UpdateDeviceAsync(device, cancellationToken);
        return Map(device);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var device = await store.GetDeviceAsync(id, cancellationToken)
            ?? throw new AttendanceSourceNotFoundException(id);
        device.SetActive(isActive, clock.GetUtcNow().UtcDateTime);
        await store.UpdateDeviceAsync(device, cancellationToken);
    }

    private string ResolveDriverCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)
            || string.Equals(code.Trim(), MockDeviceContractConstant.DriverCode, StringComparison.Ordinal)
            || !_drivers.TryGetValue(code.Trim(), out var driver))
        {
            throw new UnsupportedAttendanceDriverException(code ?? string.Empty);
        }

        return driver.DriverCode;
    }

    private static void Validate(AttendanceSourceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) throw new ArgumentException("Source code is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Source name is required.");
        if (string.IsNullOrWhiteSpace(request.Host)
            || Uri.CheckHostName(request.Host.Trim()) == UriHostNameType.Unknown)
        {
            throw new ArgumentException("Host must be an IP address or a DNS name.");
        }

        if (request.Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(request.Port));
        if (request.CommunicationKey < 0) throw new ArgumentOutOfRangeException(nameof(request.CommunicationKey));
        if (request.TimeoutSeconds is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(request.TimeoutSeconds));
        if (string.IsNullOrWhiteSpace(request.TimeZoneId)) throw new ArgumentException("Time zone is required.");

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException("Time zone is not recognized by this server.", nameof(request.TimeZoneId), exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException("Time zone data is invalid on this server.", nameof(request.TimeZoneId), exception);
        }
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private AttendanceSourceDto Map(AttendanceDevice device)
    {
        var driver = _drivers[device.DriverCode];
        return new AttendanceSourceDto(
            device.Id,
            device.Code,
            device.Name,
            device.DriverCode,
            driver.DisplayName,
            device.Host,
            device.Port,
            device.TimeoutSeconds,
            device.TimeZoneId,
            device.IsActive);
    }
}
