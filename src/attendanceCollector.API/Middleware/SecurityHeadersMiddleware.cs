namespace attendanceCollector.API.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; " +
                "object-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'";
            return Task.CompletedTask;
        });

        await next(context);
    }
}
