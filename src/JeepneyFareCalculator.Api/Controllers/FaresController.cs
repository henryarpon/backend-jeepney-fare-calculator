using JeepneyFareCalculator.Api.RateLimiting;
using JeepneyFareCalculator.Application.Fares;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JeepneyFareCalculator.Api.Controllers;

/// <summary>Prices trips for commuters. No sign-in, and nothing is stored.</summary>
[ApiController]
[Route("api/fares")]
public sealed class FaresController(CalculateFare calculateFare) : ControllerBase
{
    /// <summary>
    /// Returns the fare breakdown for a trip. An invalid request is answered with
    /// 400 ProblemDetails listing every error (<see cref="ValidationExceptionHandler"/>),
    /// and too many requests from one client with 429 (<see cref="CalculateRateLimiting"/>).
    /// </summary>
    [HttpPost("calculate")]
    [EnableRateLimiting(CalculateRateLimiting.PolicyName)]
    public async Task<FareBreakdownDto> CalculateAsync(
        CalculateFareRequest request,
        CancellationToken cancellationToken)
    {
        return await calculateFare.CalculateAsync(request, cancellationToken);
    }
}
