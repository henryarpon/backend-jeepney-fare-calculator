using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JeepneyFareCalculator.Api;

/// <summary>
/// Writes the <c>/api/health</c> response. A healthy API answers with plain
/// "Healthy"; an unhealthy one answers with ProblemDetails, like every other error.
/// </summary>
internal static class HealthCheckResponseWriter
{
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        if (report.Status != HealthStatus.Unhealthy)
        {
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(report.Status.ToString());
            return;
        }

        var failedChecks = report.Entries
            .Where(entry => entry.Value.Status == HealthStatus.Unhealthy)
            .Select(entry => $"{entry.Key}: {entry.Value.Description}");

        var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = context.Response.StatusCode,
                Title = "The API is unhealthy.",
                Detail = string.Join(" ", failedChecks),
            },
        });
    }
}
