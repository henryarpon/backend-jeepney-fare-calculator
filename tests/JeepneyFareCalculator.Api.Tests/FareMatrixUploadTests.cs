using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using JeepneyFareCalculator.Api.Tests.Infrastructure;

namespace JeepneyFareCalculator.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class FareMatrixUploadTests(ApiFactory factory)
{
    // Sample data for tests only, not official LTFRB fares.
    private const string SampleCsv =
        "DistanceKm,RegularFare,DiscountedFare\n" +
        "1,13.00,10.40\n" +
        "2,13.00,10.40\n" +
        "3,14.75,11.80\n";

    [Fact]
    public async Task Preview_ValidCsv_ReturnsParsedRows()
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsync(
            "/api/admin/fare-matrices/preview",
            UploadForm("TraditionalJeepney", SampleCsv));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<PreviewBody>();
        Assert.Equal("TraditionalJeepney", preview?.VehicleType);
        Assert.Equal(
            [new RowBody(1, 13.00m, 10.40m), new RowBody(2, 13.00m, 10.40m), new RowBody(3, 14.75m, 11.80m)],
            preview?.Rows);
    }

    [Fact]
    public async Task Preview_ValidCsv_SavesNothing()
    {
        var client = await factory.CreateSignedInClientAsync();
        var versionsBefore = await ListVersionsAsync(client, "TraditionalJeepney");

        var response = await client.PostAsync(
            "/api/admin/fare-matrices/preview",
            UploadForm("TraditionalJeepney", SampleCsv));
        var versionsAfter = await ListVersionsAsync(client, "TraditionalJeepney");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(versionsBefore, versionsAfter);
    }

    [Fact]
    public async Task Upload_ValidCsv_BecomesActiveVersionInList()
    {
        var client = await factory.CreateSignedInClientAsync();

        var uploadResponse = await client.PostAsync(
            "/api/admin/fare-matrices",
            UploadForm("TraditionalJeepney", SampleCsv, note: "LTFRB MC 2026-001 (sample)"));
        var versions = await ListVersionsAsync(client, "TraditionalJeepney");

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<VersionBody>();
        var newest = versions[0];
        Assert.Equal(uploaded?.Id, newest.Id);
        Assert.True(newest.IsActive);
        Assert.Equal("LTFRB MC 2026-001 (sample)", newest.Note);
        Assert.Equal(3, newest.RowCount);
        Assert.Equal(TimeSpan.Zero, newest.UploadedAt.Offset);
        Assert.InRange(newest.UploadedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Upload_SecondForSameVehicleType_DeactivatesFirst()
    {
        var client = await factory.CreateSignedInClientAsync();

        var first = await UploadAsync(client, "ModernJeepney", note: "first");
        var second = await UploadAsync(client, "ModernJeepney", note: "second");
        var versions = await ListVersionsAsync(client, "ModernJeepney");

        Assert.Equal([second.Id, first.Id], versions.Take(2).Select(v => v.Id));
        Assert.True(versions[0].IsActive);
        Assert.False(versions[1].IsActive);
        Assert.Single(versions, v => v.IsActive);
    }

    [Fact]
    public async Task Upload_ForOneVehicleType_LeavesOtherVehicleTypeUnchanged()
    {
        var client = await factory.CreateSignedInClientAsync();
        var modernActive = await UploadAsync(client, "ModernJeepney");
        var modernBefore = await ListVersionsAsync(client, "ModernJeepney");

        await UploadAsync(client, "TraditionalJeepney");
        var modernAfter = await ListVersionsAsync(client, "ModernJeepney");

        Assert.Equal(modernBefore, modernAfter);
        Assert.Equal(modernActive.Id, Assert.Single(modernAfter, v => v.IsActive).Id);
    }

    [Fact]
    public async Task Preview_SignedOut_Returns401()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.PostAsync(
            "/api/admin/fare-matrices/preview",
            UploadForm("TraditionalJeepney", SampleCsv));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_SignedOut_Returns401AndSavesNothing()
    {
        var signedInClient = await factory.CreateSignedInClientAsync();
        var versionsBefore = await ListVersionsAsync(signedInClient, "TraditionalJeepney");

        var response = await factory.CreateHttpsClient().PostAsync(
            "/api/admin/fare-matrices",
            UploadForm("TraditionalJeepney", SampleCsv));
        var versionsAfter = await ListVersionsAsync(signedInClient, "TraditionalJeepney");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(versionsBefore, versionsAfter);
    }

    [Fact]
    public async Task List_SignedOut_Returns401()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/admin/fare-matrices?vehicleType=TraditionalJeepney");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<VersionBody> UploadAsync(HttpClient client, string vehicleType, string? note = null)
    {
        var response = await client.PostAsync("/api/admin/fare-matrices", UploadForm(vehicleType, SampleCsv, note));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<VersionBody>())!;
    }

    private static async Task<List<VersionBody>> ListVersionsAsync(HttpClient client, string vehicleType)
    {
        var response = await client.GetAsync($"/api/admin/fare-matrices?vehicleType={vehicleType}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<List<VersionBody>>() ?? [];
    }

    private static MultipartFormDataContent UploadForm(string vehicleType, string csv, string? note = null)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        var form = new MultipartFormDataContent
        {
            { new StringContent(vehicleType), "vehicleType" },
            { file, "file", "fare-matrix.csv" },
        };
        if (note is not null)
        {
            form.Add(new StringContent(note), "note");
        }
        return form;
    }

    private sealed record RowBody(int DistanceKm, decimal RegularFare, decimal DiscountedFare);

    private sealed record PreviewBody(string VehicleType, List<RowBody> Rows);

    private sealed record VersionBody(
        int Id,
        string VehicleType,
        DateTimeOffset UploadedAt,
        string? Note,
        bool IsActive,
        int RowCount);
}
