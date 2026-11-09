using System.Diagnostics.Metrics;

namespace attendanceCollector.API.Services;

public sealed class CollectorIntegrationMetricsService : IDisposable
{
    public const string MeterName = "attendanceCollector.Integration";
    private readonly Meter meter = new(MeterName, "1.0");
    private readonly Counter<long> requests;
    private readonly Counter<long> auditFailures;
    private readonly Histogram<double> duration;

    public CollectorIntegrationMetricsService()
    {
        requests = meter.CreateCounter<long>("collector.hrm.read.requests");
        auditFailures = meter.CreateCounter<long>("collector.hrm.audit.failures");
        duration = meter.CreateHistogram<double>("collector.hrm.read.duration", "ms");
    }

    public void Record(int status, double milliseconds)
    {
        var tag = new KeyValuePair<string, object?>("http.response.status_code", status);
        requests.Add(1, tag);
        duration.Record(milliseconds, tag);
    }
    public void RecordAuditFailure() => auditFailures.Add(1);
    public void Dispose() => meter.Dispose();
}
