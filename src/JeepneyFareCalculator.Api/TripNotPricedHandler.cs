using JeepneyFareCalculator.Application.Fares;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JeepneyFareCalculator.Api;

/// <summary>
/// Answers a trip that can't be priced with 422 ProblemDetails. The machine-readable
/// reason is under <c>reason</c> (e.g. <c>"SamePoint"</c>), and <c>detail</c> is the
/// message for the commuter. A trip beyond the matrix also has <c>maxDistanceKm</c>.
/// </summary>
public sealed class TripNotPricedHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not TripNotPricedException notPriced)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "The trip can't be priced.",
            Detail = notPriced.Message,
            Extensions = { ["reason"] = notPriced.Reason.ToString() },
        };
        if (notPriced.MaxDistanceKm is { } maxDistanceKm)
        {
            problem.Extensions["maxDistanceKm"] = maxDistanceKm;
        }

        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}
