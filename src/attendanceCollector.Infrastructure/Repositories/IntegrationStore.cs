using System.Text.Json;
using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using attendanceCollector.Infrastructure.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace attendanceCollector.Infrastructure.Repositories;

public sealed class IntegrationStore(AttendanceCollectorDbContext db) : IIntegrationStore
{
    public Task<IntegrationApiKey?> GetKeyAsync(Guid keyId, CancellationToken cancellationToken) =>
        db.IntegrationApiKeys.AsNoTracking().Where(k => k.Id == keyId
            && db.IntegrationClients.Any(c => c.Id == k.ClientId && c.IsActive)).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasAccessAsync(IntegrationIdentity identity, Guid deviceId, DateTime nowUtc, CancellationToken cancellationToken) =>
        db.IntegrationApiKeys.AsNoTracking().AnyAsync(k => k.Id == identity.KeyId && k.ClientId == identity.ClientId
            && k.RevokedAtUtc == null && (k.ExpiresAtUtc == null || k.ExpiresAtUtc > nowUtc)
            && db.IntegrationClients.Any(c => c.Id == k.ClientId && c.IsActive
                && (c.Scope == CollectorIntegrationConstant.ReadScope || c.Scope == CollectorIntegrationConstant.ReadCollectScope))
            && db.IntegrationDeviceGrants.Any(g => g.ClientId == k.ClientId && g.DeviceId == deviceId), cancellationToken);

    public Task<bool> HasCollectionAccessAsync(IntegrationIdentity identity, Guid deviceId, DateTime nowUtc, CancellationToken cancellationToken) =>
        db.IntegrationApiKeys.AsNoTracking().AnyAsync(k => k.Id == identity.KeyId && k.ClientId == identity.ClientId
            && k.RevokedAtUtc == null && (k.ExpiresAtUtc == null || k.ExpiresAtUtc > nowUtc)
            && db.IntegrationClients.Any(c => c.Id == k.ClientId && c.IsActive && c.Scope == CollectorIntegrationConstant.ReadCollectScope)
            && db.IntegrationDeviceGrants.Any(g => g.ClientId == k.ClientId && g.DeviceId == deviceId), cancellationToken);

    public async Task<bool> AreDevicesValidAsync(IReadOnlyList<Guid> deviceIds, CancellationToken cancellationToken) =>
        await db.AttendanceDevices.CountAsync(d => deviceIds.Contains(d.Id), cancellationToken) == deviceIds.Count;

    private IQueryable<IntegrationClientDto> ProjectClients(IQueryable<IntegrationClient> clients) => clients.AsSplitQuery().Select(c => new IntegrationClientDto(
        c.Id, c.Name, c.IsActive, c.Version,
        db.IntegrationDeviceGrants.Where(g => g.ClientId == c.Id).OrderBy(g => g.DeviceId).Select(g => g.DeviceId).ToArray(),
        db.IntegrationApiKeys.Where(k => k.ClientId == c.Id).OrderByDescending(k => k.CreatedAtUtc).ThenBy(k => k.Id)
            .Take(100).Select(k => new IntegrationKeyDto(k.Id, k.CreatedAtUtc, k.ExpiresAtUtc, k.RevokedAtUtc)).ToArray()));

    public async Task<IntegrationClientPage> GetClientsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var count = await db.IntegrationClients.CountAsync(cancellationToken);
        var items = await ProjectClients(db.IntegrationClients.AsNoTracking().OrderBy(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToArrayAsync(cancellationToken);
        return new(items, page, pageSize, count);
    }

    public Task<IntegrationClientDto?> GetClientAsync(Guid id, CancellationToken cancellationToken) =>
        ProjectClients(db.IntegrationClients.AsNoTracking().Where(c => c.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public async Task CreateClientAsync(IntegrationClient client, IReadOnlyList<Guid> deviceIds, string actor, CancellationToken cancellationToken)
    {
        db.IntegrationClients.Add(client);
        db.IntegrationDeviceGrants.AddRange(deviceIds.Select(d => new IntegrationDeviceGrant { ClientId = client.Id, DeviceId = d }));
        Audit(client.Id, null, actor, CollectorIntegrationConstant.ClientCreated, null, JsonSerializer.Serialize(deviceIds), client.CreatedAtUtc);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceGrantsAsync(Guid clientId, Guid version, IReadOnlyList<Guid> deviceIds, string actor, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var client = await LockClientAsync(clientId, cancellationToken);
        ValidateVersion(client, version);
        var old = await db.IntegrationDeviceGrants.Where(g => g.ClientId == clientId).Select(g => g.DeviceId).ToArrayAsync(cancellationToken);
        await db.IntegrationDeviceGrants.Where(g => g.ClientId == clientId).ExecuteDeleteAsync(cancellationToken);
        db.IntegrationDeviceGrants.AddRange(deviceIds.Select(d => new IntegrationDeviceGrant { ClientId = clientId, DeviceId = d }));
        client.Touch();
        Audit(clientId, null, actor, CollectorIntegrationConstant.GrantsChanged, JsonSerializer.Serialize(old), JsonSerializer.Serialize(deviceIds), nowUtc);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid clientId, Guid version, bool active, string actor, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var client = await LockClientAsync(clientId, cancellationToken);
        ValidateVersion(client, version);
        var before = client.IsActive.ToString();
        client.SetActive(active);
        Audit(clientId, null, actor, CollectorIntegrationConstant.ActiveChanged, before, active.ToString(), nowUtc);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CreateKeyAsync(IntegrationApiKey key, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var client = await LockClientAsync(key.ClientId, cancellationToken);
        if (!client.IsActive || await db.IntegrationApiKeys.CountAsync(k => k.ClientId == client.Id && k.RevokedAtUtc == null
                && (k.ExpiresAtUtc == null || k.ExpiresAtUtc > key.CreatedAtUtc), cancellationToken) >= 2)
            throw new CollectorIntegrationException(409, CollectorIntegrationConstant.Conflict, "Enable the client and keep at most two active keys during rotation.");
        client.Touch();
        db.IntegrationApiKeys.Add(key);
        Audit(client.Id, key.Id, actor, CollectorIntegrationConstant.KeyCreated, null, null, key.CreatedAtUtc);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeKeyAsync(Guid clientId, Guid keyId, string actor, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var client = await LockClientAsync(clientId, cancellationToken);
        var key = await db.IntegrationApiKeys.SingleOrDefaultAsync(k => k.Id == keyId && k.ClientId == clientId, cancellationToken)
            ?? throw new CollectorIntegrationException(404, CollectorIntegrationConstant.NotFound, "Key was not found.");
        if (key.RevokedAtUtc is null)
        {
            key.Revoke(nowUtc);
            client.Touch();
            Audit(clientId, keyId, actor, CollectorIntegrationConstant.KeyRevoked, null, null, nowUtc);
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IntegrationClient> LockClientAsync(Guid id, CancellationToken cancellationToken) =>
        await db.IntegrationClients.FromSqlInterpolated($"SELECT * FROM \"IntegrationClients\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new CollectorIntegrationException(404, CollectorIntegrationConstant.NotFound, "Integration client was not found.");

    private static void ValidateVersion(IntegrationClient client, Guid version)
    {
        if (client.Version != version) throw new CollectorIntegrationException(409, CollectorIntegrationConstant.Conflict, "Reload the client before updating it.");
    }

    private void Audit(Guid clientId, Guid? keyId, string actor, string action, string? before, string? after, DateTime nowUtc) =>
        db.IntegrationAdminAudits.Add(new() { ClientId = clientId, KeyId = keyId, Actor = actor, Action = action,
            Before = before, After = after, OccurredAtUtc = nowUtc });
}
