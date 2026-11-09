using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using attendanceCollector.Infrastructure.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace attendanceCollector.Infrastructure.Services;

public sealed class IntegrationAuditStore(IDbContextFactory<AttendanceCollectorDbContext> factory) : IIntegrationAuditStore
{
    public async Task StartAsync(IntegrationReadAudit audit, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.IntegrationReadAudits.Add(new IntegrationReadAuditEntry { Id = audit.Id, StartedAtUtc = audit.StartedAtUtc,
            ClientId = audit.ClientId, DeviceId = audit.DeviceId, RequestId = audit.RequestId,
            FromUtc = audit.FromUtc, ToUtc = audit.ToUtc, PageSize = audit.PageSize, Outcome = CollectorIntegrationConstant.Requested });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(Guid auditId, Guid? clientId, int status, int count, long durationMilliseconds,
        string? errorCode, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var updated = await db.IntegrationReadAudits.Where(a => a.Id == auditId).ExecuteUpdateAsync(update => update
            .SetProperty(a => a.ClientId, clientId).SetProperty(a => a.HttpStatus, status).SetProperty(a => a.RowCount, count)
            .SetProperty(a => a.DurationMilliseconds, durationMilliseconds).SetProperty(a => a.ErrorCode, errorCode)
            .SetProperty(a => a.Outcome, status == 200 ? CollectorIntegrationConstant.Completed : CollectorIntegrationConstant.Failed), cancellationToken);
        if (updated != 1) throw new InvalidOperationException("Read audit was not found.");
    }
}
