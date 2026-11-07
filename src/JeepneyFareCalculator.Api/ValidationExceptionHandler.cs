using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JeepneyFareCalculator.Api;

/// <summary>
/// Answers a request that failed Application validation with 400 ProblemDetails.
/// The errors are listed under <c>errors</c>, keyed by field, the same shape that
/// model binding uses for a body it can't read (e.g. an unknown vehicle type).
/// </summary>
public sealed class ValidationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validation)
        {
            return false;
        }

        var errors = validation.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The request is invalid.",
            },
        });
    }
}
