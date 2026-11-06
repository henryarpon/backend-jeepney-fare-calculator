using System.ComponentModel.DataAnnotations;
using JeepneyFareCalculator.Application.FareMatrices;
using JeepneyFareCalculator.Domain.FareMatrices;

namespace JeepneyFareCalculator.Api.Controllers;

/// <summary>
/// Multipart body of <c>POST /api/admin/fare-matrices/preview</c>. A missing field
/// or an unknown vehicle type is answered with 400 ProblemDetails by model binding.
/// </summary>
public class FareMatrixFileForm
{
    [Required]
    public VehicleType? VehicleType { get; set; }

    [Required]
    public IFormFile? File { get; set; }
}

/// <summary>Multipart body of <c>POST /api/admin/fare-matrices</c>: the file plus an optional note.</summary>
public sealed class FareMatrixUploadForm : FareMatrixFileForm
{
    [MaxLength(UploadFareMatrix.NoteMaxLength)]
    public string? Note { get; set; }
}
