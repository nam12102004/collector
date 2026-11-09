using attendanceCollector.Application.Common.Constants;
using Microsoft.AspNetCore.Mvc;

namespace attendanceCollector.API.Helpers;

public static class IntegrationProblemHelper
{
    public static ProblemDetails Create(HttpContext context, int status, string code, string title)
    {
        context.Items["IntegrationErrorCode"] = code;
        return new ProblemDetails { Status = status, Title = title,
            Extensions = { ["errorCode"] = code, ["traceId"] = context.TraceIdentifier } };
    }

    public static Task WriteAsync(HttpContext context, int status, string code, string title, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(Create(context, status, code, title), options: null,
            contentType: "application/problem+json", cancellationToken: cancellationToken);
    }
}
