using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JeepneyFareCalculator.Application.Distances;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JeepneyFareCalculator.Infrastructure.Routing;

/// <summary>
/// Estimates road distances with the OpenRouteService directions API: the driving
/// profile, avoiding highways and tollways, since jeepneys use ordinary roads.
/// The API key is <c>OpenRouteService:ApiKey</c>, from the git-ignored
/// <c>appsettings.Local.json</c> in development.
/// </summary>
/// <remarks>
/// <para>
/// The GeoJSON flavour of the endpoint is used because it returns the route line as
/// plain <c>[longitude, latitude]</c> pairs, so no polyline decoding is needed.
/// </para>
/// <para>
/// OpenRouteService reports "no route" as 404 (error codes 2009 and 2010), and an
/// exhausted quota, a bad key or an outage as other error statuses. These become
/// <see cref="DistanceEstimate.NoRoute"/> and <see cref="DistanceEstimate.ProviderUnavailable"/>
/// rather than exceptions, so the caller can tell the commuter what happened.
/// </para>
/// </remarks>
public sealed class OpenRouteServiceDistanceEstimator(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<OpenRouteServiceDistanceEstimator> logger)
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
                $"'{ApiKeySetting}' is not configured. In development, set it in appsettings.Local.json (see back-end/CLAUDE.md).");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/directions/driving-car/geojson")
        {
            // OpenRouteService takes coordinates as [longitude, latitude].
            Content = JsonContent.Create(new DirectionsRequest(
                [[origin.Longitude, origin.Latitude], [destination.Longitude, destination.Latitude]],
                new DirectionsOptions(["highways", "tollways"]),
                Instructions: false)),
        };
        request.Headers.TryAddWithoutValidation("Authorization", apiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return DistanceEstimate.NoRoute;
            }

            if (!response.IsSuccessStatusCode)
            {
                // 401/403 also cover a wrong key, so log the status to tell that apart from an outage.
                logger.LogWarning("OpenRouteService answered {StatusCode}.", (int)response.StatusCode);
                return DistanceEstimate.ProviderUnavailable;
            }

            var directions = await response.Content.ReadFromJsonAsync<DirectionsResponse>(cancellationToken);
            var route = directions?.Features?.FirstOrDefault();
            if (route is null)
            {
                return DistanceEstimate.NoRoute;
            }

            // The summary leaves the distance out when the route has no length: both
            // points snap to the same spot on the road. That is charged as the shortest
            // trip. Identical Origin and Destination never get here (CalculateFare).
            var distanceMeters = route.Properties?.Summary?.Distance ?? 0;
            var routeLine = (route.Geometry?.Coordinates ?? [])
                .Where(position => position.Length >= 2)
                .Select(position => new Coordinates(Latitude: position[1], Longitude: position[0]))
                .ToList();

            return DistanceEstimate.Found((decimal)distanceMeters / 1000m, routeLine);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "OpenRouteService could not be reached.");
            return DistanceEstimate.ProviderUnavailable;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout ran out; the commuter did not cancel.
            logger.LogWarning(exception, "OpenRouteService timed out.");
            return DistanceEstimate.ProviderUnavailable;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "OpenRouteService sent a response that could not be read.");
            return DistanceEstimate.ProviderUnavailable;
        }
    }

    private sealed record DirectionsRequest(
        [property: JsonPropertyName("coordinates")] double[][] Coordinates,
        [property: JsonPropertyName("options")] DirectionsOptions Options,
        [property: JsonPropertyName("instructions")] bool Instructions);

    private sealed record DirectionsOptions(
        [property: JsonPropertyName("avoid_features")] string[] AvoidFeatures);

    /// <summary>A GeoJSON FeatureCollection with one Feature per route.</summary>
    private sealed record DirectionsResponse(
        [property: JsonPropertyName("features")] List<RouteFeature>? Features);

    private sealed record RouteFeature(
        [property: JsonPropertyName("properties")] RouteProperties? Properties,
        [property: JsonPropertyName("geometry")] RouteGeometry? Geometry);

    private sealed record RouteProperties(
        [property: JsonPropertyName("summary")] RouteSummary? Summary);

    /// <param name="Distance">The route's length in meters.</param>
    private sealed record RouteSummary(
        [property: JsonPropertyName("distance")] double? Distance);

    /// <param name="Coordinates">A GeoJSON LineString: <c>[longitude, latitude]</c> pairs from Origin to Destination.</param>
    private sealed record RouteGeometry(
        [property: JsonPropertyName("coordinates")] double[][]? Coordinates);
}
