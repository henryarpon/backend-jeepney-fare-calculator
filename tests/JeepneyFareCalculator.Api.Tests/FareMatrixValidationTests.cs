using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using JeepneyFareCalculator.Api.Tests.Infrastructure;

namespace JeepneyFareCalculator.Api.Tests;

/// <summary>
/// An invalid fare matrix CSV is rejected as a whole with 400 ProblemDetails that
/// lists every error. Row numbers count the header as row 1, like a spreadsheet does.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FareMatrixValidationTests(ApiFactory factory)
{
    private const string PreviewUrl = "/api/admin/fare-matrices/preview";
    private const string UploadUrl = "/api/admin/fare-matrices";
    private const string Header = "DistanceKm,RegularFare,DiscountedFare\n";

    // Sample data for tests only, not official LTFRB fares.
    private const string ValidCsv = Header + "1,13.00,10.40\n2,13.00,10.40\n3,14.75,11.80\n";

    [Fact]
    public async Task Preview_EmptyFile_Returns400WithFileError()
    {
        var errors = await PreviewRejectedAsync("");

        var error = Assert.Single(errors);
        Assert.Null(error.Row);
        Assert.Null(error.Column);
        Assert.Contains("empty", error.Message);
    }

    [Theory]
    [InlineData("DistanceKm,RegularFare,DiscountedFare\n")]
    [InlineData("DistanceKm,RegularFare,DiscountedFare\n\n  \n")]
    public async Task Preview_HeaderButNoRows_Returns400WithFileError(string csv)
    {
        var errors = await PreviewRejectedAsync(csv);

        var error = Assert.Single(errors);
        Assert.Null(error.Row);
        Assert.Null(error.Column);
        Assert.Contains("no rows", error.Message);
    }

    [Theory]
    [InlineData("Distance,Regular,Discounted\n1,13.00,10.40\n")]
    [InlineData("RegularFare,DiscountedFare,DistanceKm\n13.00,10.40,1\n")]
    [InlineData("1,13.00,10.40\n2,13.00,10.40\n")]
    public async Task Preview_WrongOrMissingHeader_Returns400WithRow1Error(string csv)
    {
        var errors = await PreviewRejectedAsync(csv);

        var error = Assert.Single(errors);
        Assert.Equal(1, error.Row);
        Assert.Null(error.Column);
        Assert.Contains("DistanceKm,RegularFare,DiscountedFare", error.Message);
    }

    [Theory]
    [InlineData("1,13.00,10.40\nx,13.00,10.40\n", 3, "DistanceKm")]
    [InlineData("1,abc,10.40\n", 2, "RegularFare")]
    [InlineData("1,13.00,10.40\n2,13.00,ten\n", 3, "DiscountedFare")]
    [InlineData("1,,10.40\n", 2, "RegularFare")]
    [InlineData("1,13.00, \n", 2, "DiscountedFare")]
    [InlineData("1,13.00,10.40\n2,13.00\n", 3, null)]
    [InlineData("1,13,00,10,40\n", 2, null)]
    public async Task Preview_MissingOrNonNumericValue_Returns400WithThatCell(
        string rows, int expectedRow, string? expectedColumn)
    {
        var errors = await PreviewRejectedAsync(Header + rows);

        var error = Assert.Single(errors);
        Assert.Equal(expectedRow, error.Row);
        Assert.Equal(expectedColumn, error.Column);
    }

    [Theory]
    [InlineData("1,13.00,10.40\n2.5,13.00,10.40\n")]
    [InlineData("1,13.00,10.40\n2.0,13.00,10.40\n")]
    public async Task Preview_DistanceNotWholeNumber_Returns400(string rows)
    {
        var errors = await PreviewRejectedAsync(Header + rows);

        var error = Assert.Single(errors);
        Assert.Equal(3, error.Row);
        Assert.Equal("DistanceKm", error.Column);
        Assert.Contains("whole number", error.Message);
    }

    [Theory]
    [InlineData("-1,13.00,10.40\n", "DistanceKm")]
    [InlineData("1,-13.00,10.40\n", "RegularFare")]
    [InlineData("1,13.00,-10.40\n", "DiscountedFare")]
    public async Task Preview_NegativeValue_Returns400WithThatCell(string rows, string expectedColumn)
    {
        var errors = await PreviewRejectedAsync(Header + rows);

        var error = Assert.Single(errors);
        Assert.Equal(2, error.Row);
        Assert.Equal(expectedColumn, error.Column);
        Assert.Contains("negative", error.Message);
    }

    [Theory]
    [InlineData("2,13.00,10.40\n3,13.00,10.40\n", 2)]
    [InlineData("0,13.00,10.40\n1,13.00,10.40\n", 2)]
    [InlineData("1,13.00,10.40\n2,13.00,10.40\n4,13.00,10.40\n5,13.00,10.40\n", 4)]
    [InlineData("1,13.00,10.40\n2,13.00,10.40\n2,13.00,10.40\n3,13.00,10.40\n", 4)]
    public async Task Preview_DistancesNotRunning1ToN_Returns400AtTheBreak(string rows, int expectedRow)
    {
        var errors = await PreviewRejectedAsync(Header + rows);

        var error = Assert.Single(errors);
        Assert.Equal(expectedRow, error.Row);
        Assert.Equal("DistanceKm", error.Column);
    }

    [Theory]
    [InlineData("1,13.00,10.40\n2,14.00,10.40\n3,13.50,10.40\n", 4, "RegularFare")]
    [InlineData("1,13.00,10.40\n2,13.00,10.00\n", 3, "DiscountedFare")]
    public async Task Preview_FareGoesDown_Returns400AtThatCell(string rows, int expectedRow, string expectedColumn)
    {
        var errors = await PreviewRejectedAsync(Header + rows);

        var error = Assert.Single(errors);
        Assert.Equal(expectedRow, error.Row);
        Assert.Equal(expectedColumn, error.Column);
    }

    [Fact]
    public async Task Preview_DiscountedFareAboveRegularFare_Returns400AtThatCell()
    {
        var errors = await PreviewRejectedAsync(Header + "1,13.00,10.40\n2,13.00,13.50\n3,14.00,13.50\n");

        var error = Assert.Single(errors);
        Assert.Equal(3, error.Row);
        Assert.Equal("DiscountedFare", error.Column);
    }

    [Fact]
    public async Task Preview_DiscountedFareEqualToRegularFare_ReturnsRows()
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsync(PreviewUrl, UploadForm("TraditionalJeepney", Header + "1,13.00,13.00\n"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Preview_SeveralUnreadableValues_ListsEveryOne()
    {
        var errors = await PreviewRejectedAsync(Header +
            "1,13.00,10.40\n" +
            "2,abc,10.40\n" +
            "3,14.00,10.40\n" +
            "4.5,,10.40\n" +
            "5,15.00\n");

        Assert.Equal(
            [(3, "RegularFare"), (5, "DistanceKm"), (5, "RegularFare"), (6, null)],
            errors.Select(e => (e.Row!.Value, e.Column)));
    }

    [Fact]
    public async Task Preview_SeveralBrokenRules_ListsEveryOne()
    {
        var errors = await PreviewRejectedAsync(Header +
            "1,13.00,10.40\n" +
            "2,12.00,12.50\n" +
            "4,14.00,11.00\n" +
            "5,-1.00,11.00\n");

        Assert.Equal(
            [(3, "RegularFare"), (3, "DiscountedFare"), (4, "DistanceKm"), (4, "DiscountedFare"), (5, "RegularFare")],
            errors.Select(e => (e.Row!.Value, e.Column)));
    }

    [Fact]
    public async Task Upload_InvalidCsv_Returns400AndKeepsActiveFareMatrix()
    {
        var client = await factory.CreateSignedInClientAsync();
        var validUpload = await client.PostAsync(UploadUrl, UploadForm("ModernJeepney", ValidCsv));
        Assert.Equal(HttpStatusCode.Created, validUpload.StatusCode);
        var versionsBefore = await ListVersionsAsync(client, "ModernJeepney");

        var response = await client.PostAsync(
            UploadUrl,
            UploadForm("ModernJeepney", Header + "1,13.00,10.40\n2,abc,10.40\n4,14.00,10.40\n"));
        var versionsAfter = await ListVersionsAsync(client, "ModernJeepney");

        var errors = await AssertRejectedAsync(response);
        Assert.Equal([(3, "RegularFare")], errors.Select(e => (e.Row!.Value, e.Column)));
        Assert.Equal(versionsBefore, versionsAfter);
    }

    [Fact]
    public async Task Upload_BrokenRule_Returns400AndSavesNothing()
    {
        var client = await factory.CreateSignedInClientAsync();
        var versionsBefore = await ListVersionsAsync(client, "TraditionalJeepney");

        var response = await client.PostAsync(
            UploadUrl,
            UploadForm("TraditionalJeepney", Header + "1,13.00,10.40\n2,13.00,10.40\n4,14.00,10.40\n"));
        var versionsAfter = await ListVersionsAsync(client, "TraditionalJeepney");

        var errors = await AssertRejectedAsync(response);
        Assert.Equal([(4, "DistanceKm")], errors.Select(e => (e.Row!.Value, e.Column)));
        Assert.Equal(versionsBefore, versionsAfter);
    }

    private static async Task<List<VersionBody>> ListVersionsAsync(HttpClient client, string vehicleType)
    {
        var response = await client.GetAsync($"/api/admin/fare-matrices?vehicleType={vehicleType}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<List<VersionBody>>() ?? [];
    }

    /// <summary>Previews the CSV, checks it was rejected with 400 ProblemDetails, and returns its row errors.</summary>
    private async Task<List<RowErrorBody>> PreviewRejectedAsync(string csv)
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsync(PreviewUrl, UploadForm("TraditionalJeepney", csv));

        return await AssertRejectedAsync(response);
    }

    private static async Task<List<RowErrorBody>> AssertRejectedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<RejectionBody>();
        Assert.Equal(400, problem?.Status);
        Assert.NotNull(problem?.RowErrors);
        Assert.All(problem.RowErrors, error => Assert.False(string.IsNullOrWhiteSpace(error.Message)));
        return problem.RowErrors;
    }

    private static MultipartFormDataContent UploadForm(string vehicleType, string csv)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        return new MultipartFormDataContent
        {
            { new StringContent(vehicleType), "vehicleType" },
            { file, "file", "fare-matrix.csv" },
        };
    }

    private sealed record RowErrorBody(int? Row, string? Column, string Message);

    private sealed record VersionBody(
        int Id,
        string VehicleType,
        DateTimeOffset UploadedAt,
        string? Note,
        bool IsActive,
        int RowCount);

    private sealed record RejectionBody(int Status, string? Title, List<RowErrorBody>? RowErrors);
}
