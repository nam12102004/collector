using System.Net;
using attendanceCollector.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using attendanceCollector.API.Helpers;

namespace attendanceCollector.API.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (CollectorIntegrationException exception)
        {
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            if (exception.RetryAfterSeconds is { } seconds)
                context.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await IntegrationProblemHelper.WriteAsync(context, exception.Status, exception.ErrorCode, exception.Message, context.RequestAborted);
        }
        catch (Exception exception)
        {
            if (context.Request.Path.StartsWithSegments("/api/v1/integration-clients"))
            {
                if (context.Response.HasStarted) throw;
                logger.LogWarning("Integration administration failed: {ErrorType} at {Origin}, trace {TraceId}", exception.GetType().Name,
                    exception.TargetSite?.DeclaringType?.FullName + "." + exception.TargetSite?.Name, context.TraceIdentifier);
                context.Response.Clear();
                await IntegrationProblemHelper.WriteAsync(context, 503,
                    attendanceCollector.Application.Common.Constants.CollectorIntegrationConstant.Unavailable,
                    "The integration administration service is unavailable.", context.RequestAborted);
                return;
            }
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            var (status, title) = exception switch
            {
                AttendanceSourceNotFoundException => ((int)HttpStatusCode.NotFound, "Attendance source was not found."),
                AttendanceSourceConflictException => ((int)HttpStatusCode.Conflict, "Attendance source code already exists."),
                UnsupportedAttendanceDriverException => ((int)HttpStatusCode.BadRequest, "Attendance driver is not supported."),
                ArgumentException => ((int)HttpStatusCode.BadRequest, "Request values are invalid."),
                _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred.")
            };

            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title
            });
        }
    }
}
