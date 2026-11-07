using System.Net;
using System.Net.Http.Json;
using JeepneyFareCalculator.Api.Tests.Infrastructure;
using JeepneyFareCalculator.Application.Distances;

namespace JeepneyFareCalculator.Api.Tests;

/// <summary>
/// Pricing before the Administrator's first upload. Unlike the other integration
/// tests, this class is not in <see cref="ApiCollection"/>: it has its own
/// <see cref="ApiFactory"/> and so its own empty database, because once a matrix is
/// uploaded no API call can take a vehicle type back to having none.
/// Nothing in this class may upload a fare matrix.
/// </summary>
[Collection(NoActiveFareMatrixCollection.Name)]
public sealed class NoActiveFareMatrixTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("TraditionalJeepney", "Traditional Jeepney")]
    [InlineData("ModernJeepney", "Modern Jeepney")]
    public async Task Calculate_VehicleTypeWithoutActiveFareMatrix_Returns422WithNoActiveFareMatrixReason(
        string vehicleType, string vehicleTypeName)
    {
        factory.DistanceEstimator.NextEstimate = DistanceEstimate.Found(5.0m);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/fares/calculate", new
        {
            vehicleType,
            origin = new { lat = 14.6191, lng = 121.0510 },
            destination = new { lat = 14.5995, lng = 120.9842 },
            party = new[] { new { passengerType = "Regular", count = 1 } },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<NotPricedProblemBody>();
        Assert.Equal("NoActiveFareMatrix", problem?.Reason);
        Assert.Equal(
            $"There are no {vehicleTypeName} fares yet. Try the other vehicle type, or check back later.",
            problem?.Detail);
    }

    private sealed record NotPricedProblemBody(int Status, string? Detail, string? Reason);
}
