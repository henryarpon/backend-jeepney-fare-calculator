using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JeepneyFareCalculator.Api.RateLimiting;

/// <summary>
/// Limits fare calculations to 30 per minute per client IP address, so one client
/// can't use up the routing provider's free quota (about 2,000 routes a day) for
/// everyone. Endpoints opt in with <c>[EnableRateLimiting(PolicyName)]</c>. A request
/// over the limit gets 429 ProblemDetails, with <c>Retry-After</c> in seconds.
/// </summary>
internal static class CalculateRateLimiting
{
    public const string PolicyName = "calculate";

    private const int PermitLimit = 30;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddCalculateRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PolicyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitLimit,
                    Window = Window,
                    // Over the limit means rejected straight away, never kept waiting.
                    QueueLimit = 0,
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    httpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests.",
                        Detail = "Too many requests, please wait a moment.",
                    },
                });
            };
        });

        return services;
    }
}
