using attendanceCollector.Application.Contracts;
using attendanceCollector.Domain.Entities;

namespace attendanceCollector.Application.Interfaces;

public interface IIntegrationRequestContext
{
    IntegrationIdentity? Identity { get; }
}

public interface ICollectorPageTokenService
{
    string Protect(CollectorPageCursor cursor);
    CollectorPageCursor Unprotect(string token);
}

public interface IIntegrationCredentialService
{
    IntegrationKeySecret Create(Guid keyId, DateTime? expiresAtUtc);
    string GetVerifier(string apiKey);
    Task<IntegrationIdentity?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken);
}

public interface IIntegrationStore
{
    Task<IntegrationApiKey?> GetKeyAsync(Guid keyId, CancellationToken cancellationToken);
    Task<bool> HasAccessAsync(IntegrationIdentity identity, Guid deviceId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<bool> HasCollectionAccessAsync(IntegrationIdentity identity, Guid deviceId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<bool> AreDevicesValidAsync(IReadOnlyList<Guid> deviceIds, CancellationToken cancellationToken);
    Task<IntegrationClientPage> GetClientsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<IntegrationClientDto?> GetClientAsync(Guid id, CancellationToken cancellationToken);
    Task CreateClientAsync(IntegrationClient client, IReadOnlyList<Guid> deviceIds, string actor, CancellationToken cancellationToken);
    Task ReplaceGrantsAsync(Guid clientId, Guid version, IReadOnlyList<Guid> deviceIds, string actor, DateTime nowUtc, CancellationToken cancellationToken);
    Task SetActiveAsync(Guid clientId, Guid version, bool active, string actor, DateTime nowUtc, CancellationToken cancellationToken);
    Task CreateKeyAsync(IntegrationApiKey key, string actor, CancellationToken cancellationToken);
    Task RevokeKeyAsync(Guid clientId, Guid keyId, string actor, DateTime nowUtc, CancellationToken cancellationToken);
}

public interface ICollectorReadStore
{
    Task<CollectorSnapshotPage> CreateSnapshotAsync(Guid clientId, CollectorReadCriteria criteria,
        DateTime nowUtc, CancellationToken cancellationToken);
    Task<CollectorSnapshotPage> ReadSnapshotAsync(CollectorPageCursor cursor, int pageSize,
        DateTime nowUtc, CancellationToken cancellationToken);
    Task<int> DeleteExpiredSnapshotsAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

public interface IIntegrationAuditStore
{
    Task StartAsync(IntegrationReadAudit audit, CancellationToken cancellationToken);
    Task CompleteAsync(Guid auditId, Guid? clientId, int status, int count, long durationMilliseconds,
        string? errorCode, CancellationToken cancellationToken);
}

public interface ICollectorAttendanceReadService
{
    Task<CollectorLogPage> ReadAsync(string deviceId, CollectorLogRequest request, CancellationToken cancellationToken);
}

public interface IIntegrationClientService
{
    Task<IntegrationClientPage> GetClientsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<IntegrationClientDto> GetClientAsync(Guid id, CancellationToken cancellationToken);
    Task<IntegrationClientDto> CreateAsync(CreateIntegrationClientRequest request, CancellationToken cancellationToken);
    Task<IntegrationClientDto> ReplaceGrantsAsync(Guid id, ReplaceIntegrationGrantsRequest request, CancellationToken cancellationToken);
    Task<IntegrationClientDto> SetActiveAsync(Guid id, SetIntegrationActiveRequest request, CancellationToken cancellationToken);
    Task<IntegrationKeySecret> CreateKeyAsync(Guid id, CreateIntegrationKeyRequest request, CancellationToken cancellationToken);
    Task RevokeKeyAsync(Guid id, Guid keyId, CancellationToken cancellationToken);
}
