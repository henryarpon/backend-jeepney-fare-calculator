using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using JeepneyFareCalculator.Api.Tests.Infrastructure;
using JeepneyFareCalculator.Application.Distances;

namespace JeepneyFareCalculator.Api.Tests;

/// <summary>
/// Protecting the routing provider's free quota: the calculate rate limit and the
/// Estimated Distance cache. Unlike most integration tests, this class is not in
/// <see cref="ApiCollection"/>: <see cref="RoutingQuotaApiFactory"/> keeps the cache,
/// so each test uses its own coordinates and never sees another test's estimate.
/// </summary>
[Collection(RoutingQuotaCollection.Name)]
public sealed class RoutingQuotaTests(RoutingQuotaApiFactory factory) : IClassFixture<RoutingQuotaApiFactory>
{
    // Sample data for tests only, not official LTFRB fares.
    private const string SampleCsv =
        "DistanceKm,RegularFare,DiscountedFare\n" +
        "1,13.00,10.40\n" +
        "2,13.00,10.40\n" +
        "3,13.00,10.40\n" +
        "4,13.00,10.40\n" +
        "5,14.75,11.80\n";

    [Fact]
    public async Task Calculate_31stRequestWithinAMinute_Returns429AsProblemDetails()
    {
        await UploadSampleMatrixAsync();
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(4.0m);
        var request = Request(
            new { lat = 7.0731, lng = 125.6128 },
            new { lat = 7.0907, lng = 125.6311 },
            ("Regular", 1));

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            var allowed = await CalculateAsync(request, clientIp: "203.0.113.10");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var rejected = await CalculateAsync(request, clientIp: "203.0.113.10");
        var otherClient = await CalculateAsync(request, clientIp: "203.0.113.20");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal(429, problem?.Status);
        Assert.Equal("Too many requests, please wait a moment.", problem?.Detail);
        Assert.Equal(HttpStatusCode.OK, otherClient.StatusCode);
    }

    [Fact]
    public async Task Calculate_SameTripWithDifferentParties_CallsEstimatorOnce()
    {
        await UploadSampleMatrixAsync();
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(5.0m);
        var callsBefore = factory.DistanceEstimator.CallCount;
        var origin = new { lat = 14.5547, lng = 121.0244 };
        var destination = new { lat = 14.5794, lng = 121.0359 };

        var first = await CalculateAsync(Request(origin, destination, ("Regular", 1)));
        var second = await CalculateAsync(Request(origin, destination, ("Regular", 1), ("Student", 2)));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, factory.DistanceEstimator.CallCount - callsBefore);
    }

    [Fact]
    public async Task Calculate_CoordinatesEqualTo4DecimalPlaces_ShareOneEstimate()
    {
        await UploadSampleMatrixAsync();
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(3.2m);
        var callsBefore = factory.DistanceEstimator.CallCount;

        // About a meter apart, so within the same 4-decimal-place cell (roughly 11 m).
        var first = await CalculateAsync(Request(
            new { lat = 14.65371, lng = 121.06842 },
            new { lat = 14.67612, lng = 121.04379 },
            ("Regular", 1)));
        var second = await CalculateAsync(Request(
            new { lat = 14.65374, lng = 121.06838 },
            new { lat = 14.67609, lng = 121.04383 },
            ("Regular", 1)));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, factory.DistanceEstimator.CallCount - callsBefore);
    }

    [Fact]
    public async Task Calculate_NoRouteResult_IsNotCached()
    {
        await UploadSampleMatrixAsync();
        var callsBefore = factory.DistanceEstimator.CallCount;
        var origin = new { lat = 10.3157, lng = 123.8854 };
        var destination = new { lat = 10.3321, lng = 123.9061 };

        factory.DistanceEstimator.NextEstimate = DistanceEstimate.NoRoute;
        var noRoute = await CalculateAsync(Request(origin, destination, ("Regular", 1)));
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(2.5m);
        var found = await CalculateAsync(Request(origin, destination, ("Regular", 1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, noRoute.StatusCode);
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(2, factory.DistanceEstimator.CallCount - callsBefore);
    }

    private async Task<HttpResponseMessage> CalculateAsync(object request, string? clientIp = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/fares/calculate")
        {
            Content = JsonContent.Create(request),
        };
        if (clientIp is not null)
        {
            message.Headers.Add(ApiFactory.ClientIpHeader, clientIp);
        }

        return await factory.CreateClient().SendAsync(message);
    }

    private static object Request(
        object origin,
        object destination,
        params (string PassengerType, int Count)[] party)
    {
        return new
        {
            vehicleType = "TraditionalJeepney",
            origin,
            destination,
            party = party.Select(member => new { passengerType = member.PassengerType, count = member.Count }),
        };
    }

    /// <summary>Uploads the sample matrix, which becomes the Traditional Jeepney Active Fare Matrix.</summary>
    private async Task UploadSampleMatrixAsync()
    {
        var client = await factory.CreateSignedInClientAsync();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(SampleCsv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        var form = new MultipartFormDataContent
        {
            { new StringContent("TraditionalJeepney"), "vehicleType" },
            { file, "file", "fare-matrix.csv" },
        };

        var response = await client.PostAsync("/api/admin/fare-matrices", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private sealed record ProblemBody(int Status, string? Title, string? Detail);
}
