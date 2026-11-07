using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using JeepneyFareCalculator.Api.Tests.Infrastructure;
using JeepneyFareCalculator.Application.Distances;

namespace JeepneyFareCalculator.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class FareCalculationTests(ApiFactory factory)
{
    // Sample data for tests only, not official LTFRB fares.
    private const string SampleCsv =
        "DistanceKm,RegularFare,DiscountedFare\n" +
        "1,13.00,10.40\n" +
        "2,13.00,10.40\n" +
        "3,13.00,10.40\n" +
        "4,13.00,10.40\n" +
        "5,14.75,11.80\n" +
        "6,16.50,13.20\n" +
        "7,18.25,14.60\n";

    [Fact]
    public async Task Calculate_WholeKmDistance_ChargesThatDistance()
    {
        var version = await UploadSampleMatrixAsync("TraditionalJeepney", note: "Sample matrix");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(5.0m);

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/fares/calculate",
            Request("TraditionalJeepney", ("Regular", 1)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var breakdown = await response.Content.ReadFromJsonAsync<BreakdownBody>();
        Assert.Equal(5.0m, breakdown?.EstimatedDistanceKm);
        Assert.Equal(5, breakdown?.ChargedDistanceKm);
        Assert.Equal([new LineBody("Regular", 14.75m, 0.00m, 14.75m, 1, 14.75m)], breakdown?.Lines);
        Assert.Equal(14.75m, breakdown?.TotalFare);
        Assert.Equal(new VersionBody(version.Id, version.UploadedAt, "Sample matrix"), breakdown?.FareMatrixVersion);
    }

    [Theory]
    [InlineData(4.3, 4.3, 5)]
    [InlineData(4.04, 4.0, 4)]
    [InlineData(0.4, 0.4, 1)]
    [InlineData(0.0, 0.0, 1)]
    public async Task Calculate_FractionalDistance_RoundsUpToChargedDistance(
        decimal providerKm,
        decimal estimatedKm,
        int chargedKm)
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(providerKm);

        var breakdown = await CalculateAsync(Request("TraditionalJeepney", ("Regular", 1)));

        Assert.Equal(estimatedKm, breakdown.EstimatedDistanceKm);
        Assert.Equal(chargedKm, breakdown.ChargedDistanceKm);
    }

    [Fact]
    public async Task Calculate_UnderOneKm_ChargesTheMinimumFare()
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(0.4m);

        var breakdown = await CalculateAsync(Request("TraditionalJeepney", ("Regular", 1)));

        Assert.Equal(13.00m, breakdown.TotalFare);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("SeniorCitizen")]
    [InlineData("Pwd")]
    public async Task Calculate_DiscountedPassengerType_PaysTheDiscountedFare(string passengerType)
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(5.0m);

        var breakdown = await CalculateAsync(Request("TraditionalJeepney", (passengerType, 1)));

        Assert.Equal([new LineBody(passengerType, 14.75m, 2.95m, 11.80m, 1, 11.80m)], breakdown.Lines);
        Assert.Equal(11.80m, breakdown.TotalFare);
    }

    [Fact]
    public async Task Calculate_MixedParty_ListsEachPassengerTypeInOrderAndAddsUpTheTotal()
    {
        await UploadSampleMatrixAsync("ModernJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(4.3m);

        var breakdown = await CalculateAsync(Request(
            "ModernJeepney",
            ("SeniorCitizen", 1),
            ("Pwd", 0),
            ("Regular", 2),
            ("Student", 1)));

        Assert.Equal(
            [
                new LineBody("Regular", 14.75m, 0.00m, 14.75m, 2, 29.50m),
                new LineBody("Student", 14.75m, 2.95m, 11.80m, 1, 11.80m),
                new LineBody("SeniorCitizen", 14.75m, 2.95m, 11.80m, 1, 11.80m),
            ],
            breakdown.Lines);
        Assert.Equal(53.10m, breakdown.TotalFare);
    }

    [Fact]
    public async Task Calculate_AnyTrip_ReturnsTheChargedRowWithTheRowsBeforeAndAfter()
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(5.0m);

        var breakdown = await CalculateAsync(Request("TraditionalJeepney", ("Regular", 1)));

        Assert.Equal(
            [new RowBody(4, 13.00m, 10.40m), new RowBody(5, 14.75m, 11.80m), new RowBody(6, 16.50m, 13.20m)],
            breakdown.MatrixExcerpt);
    }

    [Theory]
    [InlineData(0.4, new[] { 1, 2 })]
    [InlineData(7.0, new[] { 6, 7 })]
    public async Task Calculate_FirstOrLastRow_ReturnsTheOneNeighbourThereIs(decimal providerKm, int[] excerptKms)
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(providerKm);

        var breakdown = await CalculateAsync(Request("TraditionalJeepney", ("Regular", 1)));

        Assert.Equal(excerptKms, breakdown.MatrixExcerpt.Select(row => row.DistanceKm));
    }

    [Fact]
    public async Task Calculate_ActiveMatrixChanged_UsesTheNewActiveVersion()
    {
        await UploadSampleMatrixAsync("TraditionalJeepney", note: "older");
        var newer = await UploadSampleMatrixAsync("TraditionalJeepney", note: "newer");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(2.0m);

        var breakdown = await CalculateAsync(Request("TraditionalJeepney", ("Regular", 1)));

        Assert.Equal(new VersionBody(newer.Id, newer.UploadedAt, "newer"), breakdown.FareMatrixVersion);
    }

    public static TheoryData<string, string> InvalidRequests => new()
    {
        { "unknown vehicle type", Body(vehicleType: "\"Bus\"") },
        { "missing vehicle type", Body(vehicleType: "null") },
        { "origin latitude above 90", Body(origin: "{\"lat\":90.5,\"lng\":121.05}") },
        { "destination longitude below -180", Body(destination: "{\"lat\":14.6,\"lng\":-180.5}") },
        { "missing origin", Body(origin: "null") },
        { "count above 30", Body(party: "[{\"passengerType\":\"Regular\",\"count\":31}]") },
        { "negative count", Body(party: "[{\"passengerType\":\"Regular\",\"count\":1},{\"passengerType\":\"Student\",\"count\":-1}]") },
        { "unknown passenger type", Body(party: "[{\"passengerType\":\"Child\",\"count\":1}]") },
        { "empty party", Body(party: "[{\"passengerType\":\"Regular\",\"count\":0}]") },
        { "party above 30", Body(party: "[{\"passengerType\":\"Regular\",\"count\":30},{\"passengerType\":\"Student\",\"count\":1}]") },
        { "passenger type listed twice", Body(party: "[{\"passengerType\":\"Regular\",\"count\":1},{\"passengerType\":\"Regular\",\"count\":1}]") },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Calculate_InvalidRequest_Returns400AsProblemDetails(string scenario, string body)
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(5.0m);

        var response = await factory.CreateClient().PostAsync(
            "/api/fares/calculate",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{scenario}: got {response.StatusCode}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.Equal(400, problem?.Status);
        Assert.NotEmpty(problem?.Errors ?? []);
    }

    public static TheoryData<string, string> InvalidRequestKeys => new()
    {
        { Body(vehicleType: "null"), "$.vehicleType" },
        { Body(origin: "{\"lat\":90.5,\"lng\":121.05}"), "$.origin.lat" },
        { Body(party: "[{\"passengerType\":\"Regular\"}]"), "$.party[0].count" },
        { Body(party: "[{\"passengerType\":\"Regular\",\"count\":31}]"), "$.party" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequestKeys))]
    public async Task Calculate_InvalidRequest_KeysErrorsByJsonPath(string body, string expectedKey)
    {
        var response = await factory.CreateClient().PostAsync(
            "/api/fares/calculate",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.NotNull(problem);
        Assert.Contains(expectedKey, problem.Errors.Keys);
    }

    [Fact]
    public async Task Calculate_EntriesMissingPassengerType_AreNotReportedAsListedTwice()
    {
        var response = await factory.CreateClient().PostAsync(
            "/api/fares/calculate",
            new StringContent(Body(party: "[{\"count\":1},{\"count\":1}]"), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        var messages = problem?.Errors.Values.SelectMany(errors => errors).ToList() ?? [];
        Assert.Contains("The passenger type is missing.", messages);
        Assert.DoesNotContain(messages, message => message.Contains("more than once"));
    }

    [Fact]
    public async Task Calculate_SameOriginAndDestination_Returns422WithSamePointReason()
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(0.0m);
        var samePoint = "{\"lat\":14.6191,\"lng\":121.0510}";

        var problem = await NotPricedAsync(Body(origin: samePoint, destination: samePoint));

        Assert.Equal("SamePoint", problem.Reason);
    }

    [Fact]
    public async Task Calculate_NoRoadRoute_Returns422WithNoRouteReason()
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.NoRoute;

        var problem = await NotPricedAsync(Body());

        Assert.Equal("NoRoute", problem.Reason);
    }

    [Fact]
    public async Task Calculate_ChargedDistanceBeyondLastRow_Returns422WithTheMaximumKm()
    {
        // The sample matrix's last row is 7 km; 7.1 km is charged as 8 km.
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(7.1m);

        var problem = await NotPricedAsync(Body());

        Assert.Equal("BeyondFareMatrix", problem.Reason);
        Assert.Equal(7, problem.MaxDistanceKm);
        Assert.Contains("7 km", problem.Detail);
    }

    public static TheoryData<string, string> PointsOutsideThePhilippines => new()
    {
        { "origin in Singapore", Body(origin: "{\"lat\":1.2903,\"lng\":103.8520}") },
        { "destination in Tokyo", Body(destination: "{\"lat\":35.6812,\"lng\":139.7671}") },
    };

    [Theory]
    [MemberData(nameof(PointsOutsideThePhilippines))]
    public async Task Calculate_PointOutsideThePhilippines_Returns422WithOutsidePhilippinesReason(
        string scenario,
        string body)
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(5.0m);

        var problem = await NotPricedAsync(body);

        Assert.True(problem.Reason == "OutsidePhilippines", $"{scenario}: got {problem.Reason}");
    }

    [Theory]
    [InlineData(20.4486, 121.9702)] // Basco, Batanes, in the far north
    [InlineData(5.0294, 119.7736)] // Bongao, Tawi-Tawi, in the far south-west
    public async Task Calculate_PointNearTheEdgeOfThePhilippines_IsPriced(double lat, double lng)
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(5.0m);
        // Invariant culture, so the JSON has "20.4486" even where the decimal separator is a comma.
        var origin = string.Create(CultureInfo.InvariantCulture, $"{{\"lat\":{lat},\"lng\":{lng}}}");
        var destination = string.Create(CultureInfo.InvariantCulture, $"{{\"lat\":{lat + 0.01},\"lng\":{lng}}}");
        var body = Body(origin: origin, destination: destination);

        var response = await factory.CreateClient().PostAsync(
            "/api/fares/calculate",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Calculate_RoutingProviderUnavailable_Returns503WithoutAFare()
    {
        await UploadSampleMatrixAsync("TraditionalJeepney");
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.ProviderUnavailable;

        var response = await factory.CreateClient().PostAsync(
            "/api/fares/calculate",
            new StringContent(Body(), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<NotPricedProblemBody>();
        Assert.Equal(503, problem?.Status);
        Assert.Equal("Couldn't estimate the distance right now, please try again.", problem?.Detail);
    }

    /// <summary>Posts the body, checks it was answered with 422 ProblemDetails, and returns the problem.</summary>
    private async Task<NotPricedProblemBody> NotPricedAsync(string body)
    {
        var response = await factory.CreateClient().PostAsync(
            "/api/fares/calculate",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = (await response.Content.ReadFromJsonAsync<NotPricedProblemBody>())!;
        Assert.Equal(422, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail), "The problem has no human-readable message.");
        return problem;
    }

    /// <summary>A valid request body as raw JSON, with one part replaced to make it invalid.</summary>
    private static string Body(
        string vehicleType = "\"TraditionalJeepney\"",
        string origin = "{\"lat\":14.6191,\"lng\":121.0510}",
        string destination = "{\"lat\":14.5995,\"lng\":120.9842}",
        string party = "[{\"passengerType\":\"Regular\",\"count\":1}]")
    {
        return $"{{\"vehicleType\":{vehicleType},\"origin\":{origin},\"destination\":{destination},\"party\":{party}}}";
    }

    private async Task<BreakdownBody> CalculateAsync(object request)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/fares/calculate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BreakdownBody>())!;
    }

    private static object Request(string vehicleType, params (string PassengerType, int Count)[] party)
    {
        return new
        {
            vehicleType,
            origin = new { lat = 14.6191, lng = 121.0510 },
            destination = new { lat = 14.5995, lng = 120.9842 },
            party = party.Select(member => new { passengerType = member.PassengerType, count = member.Count }),
        };
    }

    /// <summary>Uploads the sample matrix, which becomes the vehicle type's Active Fare Matrix.</summary>
    private async Task<UploadedVersionBody> UploadSampleMatrixAsync(string vehicleType, string? note = null)
    {
        var client = await factory.CreateSignedInClientAsync();
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
        return (await response.Content.ReadFromJsonAsync<UploadedVersionBody>())!;
    }

    private sealed record UploadedVersionBody(int Id, DateTimeOffset UploadedAt);

    private sealed record LineBody(
        string PassengerType,
        decimal RegularFare,
        decimal Discount,
        decimal FarePerPassenger,
        int Count,
        decimal Subtotal);

    private sealed record VersionBody(int Id, DateTimeOffset UploadedAt, string? Note);

    private sealed record RowBody(int DistanceKm, decimal RegularFare, decimal DiscountedFare);

    private sealed record ValidationProblemBody(int Status, Dictionary<string, string[]> Errors);

    private sealed record NotPricedProblemBody(int Status, string? Detail, string? Reason, int? MaxDistanceKm);

    private sealed record BreakdownBody(
        decimal EstimatedDistanceKm,
        int ChargedDistanceKm,
        List<LineBody> Lines,
        decimal TotalFare,
        VersionBody FareMatrixVersion,
        List<RowBody> MatrixExcerpt);
}
