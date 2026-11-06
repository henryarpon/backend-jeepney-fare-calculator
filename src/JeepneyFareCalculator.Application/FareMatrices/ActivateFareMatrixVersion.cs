namespace JeepneyFareCalculator.Application.FareMatrices;

/// <summary>
/// Makes an existing Fare Matrix Version the Active Fare Matrix for its vehicle
/// type again, e.g. to undo a bad upload. The other vehicle type is never affected.
/// </summary>
public sealed class ActivateFareMatrixVersion(IFareMatrixStore store)
{
    /// <returns>The version, now active, or <c>null</c> when no version has this id.</returns>
    public Task<FareMatrixVersionSummary?> ActivateAsync(int id, CancellationToken cancellationToken)
    {
        return store.ActivateAsync(id, cancellationToken);
    }
}
