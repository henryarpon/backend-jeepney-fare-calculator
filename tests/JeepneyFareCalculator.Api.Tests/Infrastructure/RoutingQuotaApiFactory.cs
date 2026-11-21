using JeepneyFareCalculator.Application.Distances;
using JeepneyFareCalculator.Infrastructure.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JeepneyFareCalculator.Api.Tests.Infrastructure;

/// <summary>
/// An <see cref="ApiFactory"/> that keeps the distance cache: only the routing
/// provider behind it is the fake, so <see cref="FakeDistanceEstimator.CallCount"/>
/// shows how much routing quota the API would have used.
/// </summary>
public sealed class RoutingQuotaApiFactory : ApiFactory
{
    protected override void UseFakeDistanceEstimator(IServiceCollection services)
    {
        services.RemoveAllKeyed<IDistanceEstimator>(CachingDistanceEstimator.RoutingProviderKey);
        services.AddKeyedSingleton<IDistanceEstimator>(CachingDistanceEstimator.RoutingProviderKey, DistanceEstimator);
    }
}
