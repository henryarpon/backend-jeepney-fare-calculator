using JeepneyFareCalculator.Application.Fares;
using Microsoft.AspNetCore.Mvc;

namespace JeepneyFareCalculator.Api.Controllers;

/// <summary>Prices trips for commuters. No sign-in, and nothing is stored.</summary>
[ApiController]
[Route("api/fares")]
public sealed class FaresController(CalculateFare calculateFare) : ControllerBase
{
    /// <summary>
    /// Returns the fare breakdown for a trip. An invalid request is answered with
    /// 400 ProblemDetails listing every error (<see cref="ValidationExceptionHandler"/>).
    /// </summary>
    [HttpPost("calculate")]
    public async Task<FareBreakdownDto> CalculateAsync(
        CalculateFareRequest request,
        CancellationToken cancellationToken)
    {
        return await calculateFare.CalculateAsync(request, cancellationToken);
    }
}
