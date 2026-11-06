using JeepneyFareCalculator.Application.FareMatrices;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JeepneyFareCalculator.Api;

/// <summary>
/// Answers a rejected fare matrix file with 400 ProblemDetails. Every error is
/// listed under <c>rowErrors</c> as <c>{ row, column, message }</c>.
/// </summary>
public sealed class InvalidFareMatrixFileHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not InvalidFareMatrixFileException invalidFile)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The fare matrix file is invalid.",
                Detail = "Nothing was saved. Fix the errors listed and try again.",
                Extensions = { ["rowErrors"] = invalidFile.Errors },
            },
        });
    }
}
