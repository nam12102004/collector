using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;

namespace attendanceCollector.Application.Services;

public sealed class IntegrationClientService(IIntegrationStore store, IIntegrationCredentialService credentials,
    ICurrentUser currentUser, TimeProvider clock) : IIntegrationClientService
{
    private string Actor => currentUser.IsBootstrapAdmin && currentUser.Subject is { Length: > 0 and <= 256 } subject
        ? subject : throw new CollectorIntegrationException(403, CollectorIntegrationConstant.AccessDenied, "Access denied.");

    public Task<IntegrationClientPage> GetClientsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        _ = Actor;
        if (page is < 1 or > 100_000 || pageSize is < 1 or > 100) throw InvalidRequest();
        return store.GetClientsAsync(page, pageSize, cancellationToken);
    }

    public async Task<IntegrationClientDto> GetClientAsync(Guid id, CancellationToken cancellationToken)
    {
        _ = Actor;
        return await store.GetClientAsync(id, cancellationToken)
            ?? throw new CollectorIntegrationException(404, CollectorIntegrationConstant.NotFound, "Integration client was not found.");
    }

    public async Task<IntegrationClientDto> CreateAsync(CreateIntegrationClientRequest request, CancellationToken cancellationToken)
    {
        // 1. Validate the administrator's explicit device selection; an empty set grants nothing.
        var actor = Actor;
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 128 || request.Name.Any(char.IsControl)) throw InvalidRequest();
        await ValidateDevicesAsync(request.DeviceIds, cancellationToken);
        // 2. Persist client, grants and administration audit atomically.
        var scope = request.AllowCollection
            ? CollectorIntegrationConstant.ReadCollectScope
            : CollectorIntegrationConstant.ReadScope;
        var client = IntegrationClient.Create(request.Name.Trim(), scope, clock.GetUtcNow().UtcDateTime);
        await store.CreateClientAsync(client, request.DeviceIds, actor, cancellationToken);
        return await GetClientAsync(client.Id, cancellationToken);
    }

    public async Task<IntegrationClientDto> ReplaceGrantsAsync(Guid id, ReplaceIntegrationGrantsRequest request, CancellationToken cancellationToken)
    {
        var actor = Actor;
        await ValidateDevicesAsync(request.DeviceIds, cancellationToken);
        await store.ReplaceGrantsAsync(id, request.Version, request.DeviceIds, actor, clock.GetUtcNow().UtcDateTime, cancellationToken);
        return await GetClientAsync(id, cancellationToken);
    }

    public async Task<IntegrationClientDto> SetActiveAsync(Guid id, SetIntegrationActiveRequest request, CancellationToken cancellationToken)
    {
        var actor = Actor;
        await store.SetActiveAsync(id, request.Version, request.IsActive, actor, clock.GetUtcNow().UtcDateTime, cancellationToken);
        return await GetClientAsync(id, cancellationToken);
    }

    public async Task<IntegrationKeySecret> CreateKeyAsync(Guid id, CreateIntegrationKeyRequest request, CancellationToken cancellationToken)
    {
        // 1. Generate the secret only in memory; the store receives its verifier.
        var actor = Actor;
        var now = clock.GetUtcNow().UtcDateTime;
        if (request.ExpiresAtUtc is { } expiry && (expiry.Offset != TimeSpan.Zero || expiry.UtcDateTime <= now)) throw InvalidRequest();
        var secret = credentials.Create(Guid.NewGuid(), request.ExpiresAtUtc?.UtcDateTime);
        var key = IntegrationApiKey.Create(secret.KeyId, id, credentials.GetVerifier(secret.ApiKey), now, secret.ExpiresAtUtc);
        // 2. The row lock and audit make issuance/rotation atomic. Return plaintext exactly once.
        await store.CreateKeyAsync(key, actor, cancellationToken);
        return secret;
    }

    public Task RevokeKeyAsync(Guid id, Guid keyId, CancellationToken cancellationToken) =>
        store.RevokeKeyAsync(id, keyId, Actor, clock.GetUtcNow().UtcDateTime, cancellationToken);

    private async Task ValidateDevicesAsync(IReadOnlyList<Guid>? deviceIds, CancellationToken cancellationToken)
    {
        if (deviceIds is null || deviceIds.Count > 1000 || deviceIds.Any(x => x == Guid.Empty)
            || deviceIds.Distinct().Count() != deviceIds.Count || !await store.AreDevicesValidAsync(deviceIds, cancellationToken))
            throw InvalidRequest();
    }

    private static CollectorIntegrationException InvalidRequest() =>
        new(400, CollectorIntegrationConstant.InvalidRequest, "Request values are invalid.");
}
