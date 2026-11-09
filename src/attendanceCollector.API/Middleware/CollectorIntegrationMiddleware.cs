using System.Diagnostics;
using attendanceCollector.API.Helpers;
using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Application.Validators;
using attendanceCollector.API.Services;

namespace attendanceCollector.API.Middleware;

public sealed class CollectorIntegrationMiddleware(RequestDelegate next, CollectorIntegrationOptions options,
    IWebHostEnvironment environment, IConfiguration configuration, ILogger<CollectorIntegrationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IIntegrationAuditStore audits, IIntegrationRequestContext identity, TimeProvider clock,
        CollectorIntegrationMetricsService metrics)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/devices"))
        {
            if (context.Request.Path.StartsWithSegments("/api/v1/integration-clients")) context.Response.Headers.CacheControl = "no-store";
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        if (!options.Enabled)
        {
            await IntegrationProblemHelper.WriteAsync(context, 503, CollectorIntegrationConstant.Unavailable, "Collector integration is disabled.", context.RequestAborted);
            return;
        }

        var originalAbort = context.RequestAborted;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(originalAbort);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
        context.RequestAborted = deadline.Token;
        var auditId = Guid.NewGuid();
        var started = Stopwatch.GetTimestamp();
        var auditStarted = false;
        var destination = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            try
            {
                // Audit before authentication and querying. Only validated metadata is recorded.
                var parts = context.Request.Path.Value!.Split('/');
                Guid? device = parts.Length > 4 && Guid.TryParseExact(parts[4], "D", out var parsed) ? parsed : null;
                var from = CollectorLogRequestValidator.TryParseUtc(context.Request.Query["fromUtc"].ToString(), out var f) ? f : (DateTime?)null;
                var to = CollectorLogRequestValidator.TryParseUtc(context.Request.Query["toUtc"].ToString(), out var t) ? t : (DateTime?)null;
                int? size = !context.Request.Query.ContainsKey("pageSize") ? 500
                    : int.TryParse(context.Request.Query["pageSize"].ToString(), out var s) && s is >= 1 and <= 500 ? s : null;
                await audits.StartAsync(new(auditId, clock.GetUtcNow().UtcDateTime, null, device, context.TraceIdentifier, from, to, size), deadline.Token);
                auditStarted = true;

                var allowDevelopmentHttp = environment.IsDevelopment() && configuration.GetValue<bool>("CollectorIntegration:AllowDevelopmentHttp");
                if (!context.Request.IsHttps && !allowDevelopmentHttp)
                    throw new CollectorIntegrationException(400, CollectorIntegrationConstant.InvalidRequest, "HTTPS is required.");
                string[] allowed = ["fromUtc", "toUtc", "pageSize", "pageToken"];
                if (context.Request.Query.Any(q => q.Value.Count != 1 || !allowed.Contains(q.Key, StringComparer.Ordinal)))
                    throw new CollectorIntegrationException(400, CollectorIntegrationConstant.InvalidRequest, "Request values are invalid.");
                // MVC normalizes empty/whitespace strings to null; do not interpret an empty token as a new read.
                if (context.Request.Query.TryGetValue("pageToken", out var tokenValues)
                    && (string.IsNullOrWhiteSpace(tokenValues[0]) || tokenValues[0]!.Length > CollectorIntegrationConstant.MaximumTokenLength
                        || tokenValues[0]!.Any(char.IsControl)))
                    throw new CollectorIntegrationException(400, CollectorIntegrationConstant.InvalidToken, "The page token is invalid.");

                await next(context);
                deadline.Token.ThrowIfCancellationRequested();
                if (buffer.Length > options.MaximumResponseBytes)
                    throw new CollectorIntegrationException(503, CollectorIntegrationConstant.Capacity, "The response exceeds the configured byte limit.");
            }
            catch (OperationCanceledException) when (originalAbort.IsCancellationRequested)
            {
                context.Response.StatusCode = 499;
                context.Items["IntegrationErrorCode"] = "REQUEST_CANCELLED";
                buffer.SetLength(0);
            }
            catch (Exception exception)
            {
                buffer.SetLength(0);
                context.Response.Headers.Remove("Content-Length");
                var failure = exception as CollectorIntegrationException;
                if (!auditStarted) metrics.RecordAuditFailure();
                var status = failure?.Status ?? 503;
                var code = failure?.ErrorCode ?? CollectorIntegrationConstant.Unavailable;
                // Provider/transport exception messages may include sensitive database values.
                if (failure is null) logger.LogWarning("Collector request failed: {ErrorType}, trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
                if (failure?.RetryAfterSeconds is { } retry) context.Response.Headers.RetryAfter = retry.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await IntegrationProblemHelper.WriteAsync(context, status, code, failure?.Message ?? "The read service is unavailable.", originalAbort);
            }
            finally
            {
                context.RequestAborted = originalAbort;
            }

            // Independent, bounded audit completion must succeed before any raw response bytes leave the server.
            try
            {
                if (auditStarted)
                {
                    using var auditDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await audits.CompleteAsync(auditId, identity.Identity?.ClientId, context.Response.StatusCode,
                        context.Response.StatusCode == 200 && context.Items["IntegrationRowCount"] is int rows ? rows : 0,
                        (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                        context.Items["IntegrationErrorCode"] as string, auditDeadline.Token);
                }
            }
            catch (Exception exception)
            {
                metrics.RecordAuditFailure();
                logger.LogWarning("Collector audit completion failed: {ErrorType}, trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
                buffer.SetLength(0);
                context.Response.Headers.Remove("Content-Length");
                if (!originalAbort.IsCancellationRequested)
                    await IntegrationProblemHelper.WriteAsync(context, 503, CollectorIntegrationConstant.Unavailable, "The read audit is unavailable.", originalAbort);
            }

            context.Response.Headers.CacheControl = "no-store";
            metrics.Record(context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            context.Response.Body = destination;
            if (!originalAbort.IsCancellationRequested)
            {
                buffer.Position = 0;
                context.Response.ContentLength = buffer.Length;
                await buffer.CopyToAsync(destination, originalAbort);
            }
        }
        finally
        {
            context.Response.Body = destination;
            context.RequestAborted = originalAbort;
        }
    }
}
