using System.ComponentModel.DataAnnotations;
using JeepneyFareCalculator.Application.FareMatrices;
using JeepneyFareCalculator.Domain.FareMatrices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JeepneyFareCalculator.Api.Controllers;

/// <summary>Previews, uploads, lists, shows and reactivates Fare Matrix Versions for the Administrator. Every action needs a signed-in session.</summary>
[ApiController]
[Route("api/admin/fare-matrices")]
[Authorize]
public sealed class FareMatricesController(
    PreviewFareMatrix previewFareMatrix,
    UploadFareMatrix uploadFareMatrix,
    ListFareMatrixVersions listFareMatrixVersions,
    GetFareMatrixVersion getFareMatrixVersion,
    ActivateFareMatrixVersion activateFareMatrixVersion,
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

    /// <summary>Returns one Fare Matrix Version with its rows, or 404 when there is none with this id.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FareMatrixVersionDetails>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var version = await getFareMatrixVersion.GetAsync(id, cancellationToken);
        return version is null ? NotFound() : version;
    }

    /// <summary>
    /// Makes a Fare Matrix Version the Active Fare Matrix for its vehicle type again,
    /// e.g. to undo a bad upload, and returns it. Activating the version that is
    /// already active changes nothing. 404 when there is no version with this id.
    /// </summary>
    [HttpPost("{id:int}/activate")]
    public async Task<ActionResult<FareMatrixVersionSummary>> ActivateAsync(int id, CancellationToken cancellationToken)
    {
        var version = await activateFareMatrixVersion.ActivateAsync(id, cancellationToken);
        if (version is null)
        {
            return NotFound();
        }

        logger.LogInformation(
            "Fare Matrix Version {VersionId} is now the active version for {VehicleType}.",
            version.Id, version.VehicleType);
        return version;
    }
}
