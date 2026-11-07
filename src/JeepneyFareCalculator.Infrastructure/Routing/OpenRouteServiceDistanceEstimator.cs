using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JeepneyFareCalculator.Application.Distances;
using Microsoft.Extensions.Configuration;

namespace JeepneyFareCalculator.Infrastructure.Routing;

/// <summary>
/// Estimates road distances with the OpenRouteService directions API: the driving
/// profile, avoiding highways and tollways, since jeepneys use ordinary roads.
/// The API key is <c>OpenRouteService:ApiKey</c>, from user-secrets in development.
/// </summary>
public sealed class OpenRouteServiceDistanceEstimator(HttpClient httpClient, IConfiguration configuration)
    : IDistanceEstimator
{
    public const string BaseAddress = "https://api.openrouteservice.org/";
    public const string ApiKeySetting = "OpenRouteService:ApiKey";

    public async Task<DistanceEstimate> EstimateAsync(
        Coordinates origin,
        Coordinates destination,
        CancellationToken cancellationToken)
    {
        // Read on each call rather than at start-up, so the API starts (and the
        // admin screens work) before the key is set.
        var apiKey = configuration[ApiKeySetting];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"'{ApiKeySetting}' is not configured. In development, set it with dotnet user-secrets (see back-end/CLAUDE.md).");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/directions/driving-car")
        {
            // OpenRouteService takes coordinates as [longitude, latitude].
            Content = JsonContent.Create(new DirectionsRequest(
                [[origin.Longitude, origin.Latitude], [destination.Longitude, destination.Latitude]],
                new DirectionsOptions(["highways", "tollways"]),
                Instructions: false,
                Geometry: false)),
        };
        request.Headers.TryAddWithoutValidation("Authorization", apiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var directions = await response.Content.ReadFromJsonAsync<DirectionsResponse>(cancellationToken);
        var route = directions?.Routes?.FirstOrDefault();
        if (route is null)
        {
            return DistanceEstimate.NoRoute;
        }

        // The summary leaves the distance out when the route has no length.
        var distanceMeters = route.Summary?.Distance ?? 0;
        return DistanceEstimate.Found((decimal)distanceMeters / 1000m);
    }

    private sealed record DirectionsRequest(
        [property: JsonPropertyName("coordinates")] double[][] Coordinates,
        [property: JsonPropertyName("options")] DirectionsOptions Options,
        [property: JsonPropertyName("instructions")] bool Instructions,
        [property: JsonPropertyName("geometry")] bool Geometry);

    private sealed record DirectionsOptions(
        [property: JsonPropertyName("avoid_features")] string[] AvoidFeatures);

    private sealed record DirectionsResponse(
        [property: JsonPropertyName("routes")] List<Route>? Routes);

    private sealed record Route(
        [property: JsonPropertyName("summary")] RouteSummary? Summary);

    /// <param name="Distance">The route's length in meters.</param>
    private sealed record RouteSummary(
        [property: JsonPropertyName("distance")] double? Distance);
}
