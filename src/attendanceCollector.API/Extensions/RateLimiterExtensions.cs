using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace attendanceCollector.API.Extensions;

public static class RateLimiterExtensions
{
    public const string DefaultPolicy = "default";

    public static IServiceCollection AddApiRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter(DefaultPolicy, limiter =>
            {
                limiter.PermitLimit = 100;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = true;
            });
        });

        return services;
    }
}

