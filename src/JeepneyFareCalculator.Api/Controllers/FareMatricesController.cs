using System.ComponentModel.DataAnnotations;
using JeepneyFareCalculator.Application.FareMatrices;
using JeepneyFareCalculator.Domain.FareMatrices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JeepneyFareCalculator.Api.Controllers;

/// <summary>Previews, uploads and lists Fare Matrix Versions for the Administrator. Every action needs a signed-in session.</summary>
[ApiController]
[Route("api/admin/fare-matrices")]
[Authorize]
public sealed class FareMatricesController(
    PreviewFareMatrix previewFareMatrix,
    UploadFareMatrix uploadFareMatrix,
    ListFareMatrixVersions listFareMatrixVersions,
    ILogger<FareMatricesController> logger) : ControllerBase
{
    /// <summary>
    /// Returns the rows of an uploaded CSV without saving anything. An invalid file
    /// is answered with 400 ProblemDetails listing every error (<see cref="InvalidFareMatrixFileHandler"/>).
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<FareMatrixPreview>> PreviewAsync(
        [FromForm] FareMatrixFileForm form,
        CancellationToken cancellationToken)
    {
        await using var csv = form.File!.OpenReadStream();
        return await previewFareMatrix.PreviewAsync(form.VehicleType!.Value, csv, cancellationToken);
    }

    /// <summary>
    /// Saves an uploaded CSV as a new Fare Matrix Version and makes it the vehicle
    /// type's Active Fare Matrix. The file is sent again after the preview: the
    /// server keeps no draft in between. An invalid file is rejected with 400 like
    /// the preview, and nothing is saved.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<FareMatrixVersionSummary>> UploadAsync(
        [FromForm] FareMatrixUploadForm form,
        CancellationToken cancellationToken)
    {
        await using var csv = form.File!.OpenReadStream();
        var version = await uploadFareMatrix.UploadAsync(form.VehicleType!.Value, csv, form.Note, cancellationToken);

        logger.LogInformation(
            "Fare Matrix Version {VersionId} uploaded for {VehicleType} with {RowCount} rows; it is now active.",
            version.Id, version.VehicleType, version.RowCount);
        return StatusCode(StatusCodes.Status201Created, version);
    }

    /// <summary>Lists a vehicle type's Fare Matrix Versions, newest first.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<FareMatrixVersionSummary>> ListAsync(
        [Required] VehicleType? vehicleType,
        CancellationToken cancellationToken)
    {
        return await listFareMatrixVersions.ListAsync(vehicleType!.Value, cancellationToken);
    }
}
