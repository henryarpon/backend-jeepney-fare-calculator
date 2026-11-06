namespace JeepneyFareCalculator.Api.Tests.Infrastructure;

/// <summary>
/// Shares one <see cref="ApiFactory"/> (and so one throwaway database) across
/// every test class in the run. Put <c>[Collection(ApiCollection.Name)]</c> on
/// each integration test class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
