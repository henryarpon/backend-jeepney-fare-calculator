using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using JeepneyFareCalculator.Api.Tests.Infrastructure;

namespace JeepneyFareCalculator.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class FareMatrixVersionTests(ApiFactory factory)
{
    // Sample data for tests only, not official LTFRB fares.
    private const string SampleCsv =
        "DistanceKm,RegularFare,DiscountedFare\n" +
        "1,13.00,10.40\n" +
        "2,13.00,10.40\n" +
        "3,14.75,11.80\n";

    [Fact]
    public async Task Get_UploadedVersion_ReturnsItsDetailsAndRows()
    {
        var client = await factory.CreateSignedInClientAsync();
        var uploaded = await UploadAsync(client, "TraditionalJeepney", note: "LTFRB MC 2026-002 (sample)");

        var response = await client.GetAsync($"/api/admin/fare-matrices/{uploaded.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<VersionDetailsBody>();
        Assert.Equal(uploaded.Id, version?.Id);
        Assert.Equal("TraditionalJeepney", version?.VehicleType);
        Assert.Equal(uploaded.UploadedAt, version?.UploadedAt);
        Assert.Equal("LTFRB MC 2026-002 (sample)", version?.Note);
        Assert.True(version?.IsActive);
        Assert.Equal(
            [new RowBody(1, 13.00m, 10.40m), new RowBody(2, 13.00m, 10.40m), new RowBody(3, 14.75m, 11.80m)],
            version?.Rows);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404AsProblemDetails()
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.GetAsync($"/api/admin/fare-matrices/{UnknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Activate_OlderVersion_MakesItTheOnlyActiveVersion()
    {
        var client = await factory.CreateSignedInClientAsync();
        var older = await UploadAsync(client, "TraditionalJeepney", note: "older");
        var newer = await UploadAsync(client, "TraditionalJeepney", note: "newer");

        var response = await client.PostAsync($"/api/admin/fare-matrices/{older.Id}/activate", null);
        var versions = await ListVersionsAsync(client, "TraditionalJeepney");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activated = await response.Content.ReadFromJsonAsync<VersionBody>();
        Assert.Equal(older with { IsActive = true }, activated);
        Assert.Equal(older.Id, Assert.Single(versions, v => v.IsActive).Id);
        Assert.False(versions.Single(v => v.Id == newer.Id).IsActive);
    }

    [Fact]
    public async Task Activate_ForOneVehicleType_LeavesOtherVehicleTypeUnchanged()
    {
        var client = await factory.CreateSignedInClientAsync();
        var traditionalOlder = await UploadAsync(client, "TraditionalJeepney");
        await UploadAsync(client, "TraditionalJeepney");
        await UploadAsync(client, "ModernJeepney");
        var modernBefore = await ListVersionsAsync(client, "ModernJeepney");

        var response = await client.PostAsync($"/api/admin/fare-matrices/{traditionalOlder.Id}/activate", null);
        var modernAfter = await ListVersionsAsync(client, "ModernJeepney");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(modernBefore, modernAfter);
    }

    [Fact]
    public async Task Activate_AlreadyActiveVersion_SucceedsAndChangesNothing()
    {
        var client = await factory.CreateSignedInClientAsync();
        var active = await UploadAsync(client, "ModernJeepney");
        var versionsBefore = await ListVersionsAsync(client, "ModernJeepney");

        var response = await client.PostAsync($"/api/admin/fare-matrices/{active.Id}/activate", null);
        var versionsAfter = await ListVersionsAsync(client, "ModernJeepney");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(active, await response.Content.ReadFromJsonAsync<VersionBody>());
        Assert.Equal(versionsBefore, versionsAfter);
    }

    [Fact]
    public async Task Activate_UnknownId_Returns404AsProblemDetails()
    {
        var client = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsync($"/api/admin/fare-matrices/{UnknownId}/activate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_SignedOut_Returns401()
    {
        var uploaded = await UploadAsync(await factory.CreateSignedInClientAsync(), "TraditionalJeepney");

        var response = await factory.CreateHttpsClient().GetAsync($"/api/admin/fare-matrices/{uploaded.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Activate_SignedOut_Returns401AndChangesNothing()
    {
        var signedInClient = await factory.CreateSignedInClientAsync();
        var older = await UploadAsync(signedInClient, "TraditionalJeepney");
        await UploadAsync(signedInClient, "TraditionalJeepney");
        var versionsBefore = await ListVersionsAsync(signedInClient, "TraditionalJeepney");

        var response = await factory.CreateHttpsClient().PostAsync($"/api/admin/fare-matrices/{older.Id}/activate", null);
        var versionsAfter = await ListVersionsAsync(signedInClient, "TraditionalJeepney");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(versionsBefore, versionsAfter);
    }

    /// <summary>An id no version has: ids start at 1 and the tests never upload this many.</summary>
    private const int UnknownId = int.MaxValue;

    private static async Task<VersionBody> UploadAsync(
        HttpClient client,
        string vehicleType,
        string? note = null)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(SampleCsv));
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

        var response = await client.PostAsync("/api/admin/fare-matrices", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<VersionBody>())!;
    }

    private static async Task<List<VersionBody>> ListVersionsAsync(HttpClient client, string vehicleType)
    {
        var response = await client.GetAsync($"/api/admin/fare-matrices?vehicleType={vehicleType}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<List<VersionBody>>() ?? [];
    }

    private sealed record RowBody(int DistanceKm, decimal RegularFare, decimal DiscountedFare);

    private sealed record VersionBody(
        int Id,
        string VehicleType,
        DateTimeOffset UploadedAt,
        string? Note,
        bool IsActive,
        int RowCount);

    private sealed record VersionDetailsBody(
        int Id,
        string VehicleType,
        DateTimeOffset UploadedAt,
        string? Note,
        bool IsActive,
        List<RowBody> Rows);
}
