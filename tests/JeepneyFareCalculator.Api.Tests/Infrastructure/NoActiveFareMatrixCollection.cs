namespace JeepneyFareCalculator.Api.Tests.Infrastructure;

/// <summary>
/// The collection for <c>NoActiveFareMatrixTests</c>, which needs its own
/// <see cref="ApiFactory"/> and database. It runs on its own, never alongside
/// <see cref="ApiCollection"/>, so the run still uses one host and one database at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NoActiveFareMatrixCollection
{
    public const string Name = "NoActiveFareMatrix";
}
