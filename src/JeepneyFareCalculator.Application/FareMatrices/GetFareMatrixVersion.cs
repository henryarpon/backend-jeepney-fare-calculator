namespace JeepneyFareCalculator.Application.FareMatrices;

/// <summary>Reads one Fare Matrix Version with its rows, so the Administrator can check it.</summary>
public sealed class GetFareMatrixVersion(IFareMatrixStore store)
{
    /// <returns>The version, or <c>null</c> when no version has this id.</returns>
    public Task<FareMatrixVersionDetails?> GetAsync(int id, CancellationToken cancellationToken)
    {
        return store.GetVersionAsync(id, cancellationToken);
    }
}
