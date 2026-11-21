namespace JeepneyFareCalculator.Api.Tests.Infrastructure;

/// <summary>
/// The collection for <c>RoutingQuotaTests</c>, which needs its own
/// <see cref="RoutingQuotaApiFactory"/> (and database) so the distance cache and
/// rate limiter run for real. It runs on its own, never alongside another
/// collection, so the run still uses one host and one database at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RoutingQuotaCollection
{
    public const string Name = "RoutingQuota";
}
