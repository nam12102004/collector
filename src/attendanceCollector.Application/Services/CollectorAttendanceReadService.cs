using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Application.Validators;

namespace attendanceCollector.Application.Services;

public sealed class CollectorAttendanceReadService(IIntegrationRequestContext context, IIntegrationStore integrations,
    ICollectorReadStore reads, ICollectorPageTokenService tokens, TimeProvider clock) : ICollectorAttendanceReadService
{
    public async Task<CollectorLogPage> ReadAsync(string deviceId, CollectorLogRequest request, CancellationToken cancellationToken)
    {
        // 1. Validate the fixed contract and authorize this device using current database state.
        var criteria = CollectorLogRequestValidator.Validate(deviceId, request);
        var identity = context.Identity
            ?? throw new CollectorIntegrationException(401, CollectorIntegrationConstant.InvalidCredential, "Authentication failed.");
        await ValidateAccessAsync(identity, criteria.DeviceId, cancellationToken);

        // 2. Create an immutable read set or resume exactly the set and filter protected by the token.
        CollectorSnapshotPage page;
        if (request.PageToken is null)
        {
            page = await reads.CreateSnapshotAsync(identity.ClientId, criteria, clock.GetUtcNow().UtcDateTime, cancellationToken);
        }
        else
        {
            var cursor = tokens.Unprotect(request.PageToken);
            if (cursor.Version != 1 || cursor.ClientId != identity.ClientId || cursor.DeviceId != criteria.DeviceId
                || cursor.FilterHash != criteria.FilterHash || cursor.Ordinal < 0 || cursor.SnapshotId == Guid.Empty)
                throw new CollectorIntegrationException(400, CollectorIntegrationConstant.InvalidToken, "The page token is invalid.");
            if (cursor.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
                throw new CollectorIntegrationException(400, CollectorIntegrationConstant.ExpiredToken, "The page token has expired. Start a new read.");
            page = await reads.ReadSnapshotAsync(cursor, criteria.PageSize, clock.GetUtcNow().UtcDateTime, cancellationToken);
        }

        // 3. Recheck revocation after querying and return only the six approved device fields.
        await ValidateAccessAsync(identity, criteria.DeviceId, cancellationToken);
        var next = page.HasMore ? tokens.Protect(new(1, identity.ClientId, criteria.DeviceId, criteria.FilterHash,
            page.SnapshotId, page.LastOrdinal, page.ExpiresAtUtc)) : null;
        return new(deviceId, page.Items, next);
    }

    private async Task ValidateAccessAsync(IntegrationIdentity identity, Guid deviceId, CancellationToken cancellationToken)
    {
        if (!await integrations.HasAccessAsync(identity, deviceId, clock.GetUtcNow().UtcDateTime, cancellationToken))
            throw new CollectorIntegrationException(403, CollectorIntegrationConstant.AccessDenied, "Access denied.");
    }
}
