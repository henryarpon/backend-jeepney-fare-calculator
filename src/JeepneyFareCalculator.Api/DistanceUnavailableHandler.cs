using JeepneyFareCalculator.Application.Distances;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JeepneyFareCalculator.Api;

/// <summary>
/// Answers a calculation whose distance could not be estimated, because the routing
/// provider is down, timed out or is out of quota, with 503 ProblemDetails.
/// </summary>
public sealed class DistanceUnavailableHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DistanceUnavailableException unavailable)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "The routing provider is unavailable.",
                Detail = unavailable.Message,
            },
        });
    }
}
