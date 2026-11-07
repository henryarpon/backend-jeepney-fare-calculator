using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JeepneyFareCalculator.Api;

/// <summary>
/// Answers a request that failed Application validation with 400 ProblemDetails.
/// The errors are listed under <c>errors</c>, keyed by the field's JSON path
/// (e.g. <c>$.party[0].count</c>), the same keys model binding uses for a body
/// it can't read (e.g. <c>$.vehicleType</c> for an unknown vehicle type).
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
            .GroupBy(error => ToJsonPath(error.PropertyName))
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

    /// <summary>"Party[0].Count" becomes "$.party[0].count", matching the camelCase JSON body.</summary>
    private static string ToJsonPath(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? "$"
            : "$." + string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
