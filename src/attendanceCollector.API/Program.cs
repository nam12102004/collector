using attendanceCollector.API.Extensions;
using attendanceCollector.API.Middleware;
using attendanceCollector.API.Services;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Application.Services.Users;
using attendanceCollector.Application.Services;
using attendanceCollector.Infrastructure;
using attendanceCollector.API.Helpers;
using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Infrastructure.Persistence.DbContext;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC's antiforgery authorization filter is registered by AddControllersWithViews.
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<GetCurrentUserQueryHandler>();
builder.Services.AddScoped<IAuthenticationFlowService, AuthenticationFlowService>();
builder.Services.AddScoped<IAttendanceSourceService, AttendanceSourceService>();
builder.Services.AddScoped<IAttendanceCollectionService, AttendanceCollectionService>();
builder.Services.AddScoped<IAttendanceIngestionService, AttendanceIngestionService>();
builder.Services.AddHostedService<HourlyAttendanceCollectionWorker>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthentication()
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        MockDeviceApiKeyAuthenticationHandler>(MockDeviceApiKeyAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddApiAuthorization(builder.Configuration);
builder.Services.AddApiRateLimiter();
builder.Services.AddCollectorIntegration(builder.Configuration, builder.Environment);

var app = builder.Build();

// Resolve security configuration at startup, after all configuration providers have been applied.
_ = app.Services.GetRequiredService<ICollectorPageTokenService>();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", app.Environment.IsDevelopment()))
    await app.Services.ApplyAttendanceMigrationsAsync();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }))
    .AllowAnonymous();

app.UseRouting();
app.UseMiddleware<CollectorIntegrationMiddleware>();
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/v1/integration-clients")
    || context.Request.Path.StartsWithSegments("/api/v1/devices"), branch =>
    branch.UseStatusCodePages(status =>
    {
        var responseCode = status.HttpContext.Response.StatusCode;
        var errorCode = responseCode == 401 ? CollectorIntegrationConstant.InvalidCredential
            : responseCode == 403 ? CollectorIntegrationConstant.AccessDenied : CollectorIntegrationConstant.InvalidRequest;
        return IntegrationProblemHelper.WriteAsync(status.HttpContext, responseCode, errorCode,
            "The integration administration request could not be completed.", status.HttpContext.RequestAborted);
    }));
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api/v1/devices"), branch => branch.UseHttpsRedirection());
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers().Add(endpoint =>
{
    if (!endpoint.Metadata.OfType<EnableRateLimitingAttribute>().Any())
        endpoint.Metadata.Add(new EnableRateLimitingAttribute(RateLimiterExtensions.DefaultPolicy));
});
app.MapGet("/health/ready", async (IDbContextFactory<AttendanceCollectorDbContext> factory, CancellationToken cancellationToken) =>
{
    try
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        return pending.Any() ? Results.StatusCode(503) : Results.Ok(new { status = "Ready" });
    }
    catch { return Results.StatusCode(503); }
}).AllowAnonymous();
app.MapFallback("/api/{**path}", context => IntegrationProblemHelper.WriteAsync(context, 404,
    CollectorIntegrationConstant.NotFound, "API endpoint was not found.", context.RequestAborted)).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program { }
