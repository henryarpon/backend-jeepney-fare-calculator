namespace JeepneyFareCalculator.Domain.Fares;

/// <summary>
/// All the passengers taking the same trip together, counted per passenger type.
/// Each type has 0 to <see cref="MaxPerPassengerType"/> passengers, and the Party
/// has 1 to <see cref="MaxPassengers"/> in total.
/// </summary>
public sealed class Party
{
    public const int MaxPerPassengerType = 30;
    public const int MinPassengers = 1;
    public const int MaxPassengers = 30;

    private readonly IReadOnlyDictionary<PassengerType, int> _counts;

    /// <exception cref="ArgumentException">
    /// A passenger type appears twice, a count is outside 0 to 30, or the total is outside 1 to 30.
    /// </exception>
    public Party(IEnumerable<(PassengerType PassengerType, int Count)> counts)
    {
        var byType = new Dictionary<PassengerType, int>();
        foreach (var (passengerType, count) in counts)
        {
            if (!byType.TryAdd(passengerType, count))
            {
                throw new ArgumentException($"{passengerType} appears more than once.", nameof(counts));
            }

            if (count is < 0 or > MaxPerPassengerType)
            {
                throw new ArgumentException(
                    $"{passengerType} has {count} passengers; each type has 0 to {MaxPerPassengerType}.", nameof(counts));
            }
        }

        var total = byType.Values.Sum();
        if (total is < MinPassengers or > MaxPassengers)
        {
            throw new ArgumentException(
                $"The party has {total} passengers; it must have {MinPassengers} to {MaxPassengers}.", nameof(counts));
        }

        _counts = byType;
    }

    /// <summary>The passenger types with at least one passenger, in <see cref="PassengerType"/> order.</summary>
    public IReadOnlyList<(PassengerType PassengerType, int Count)> Members =>
        _counts
            .Where(entry => entry.Value > 0)
            .OrderBy(entry => entry.Key)
            .Select(entry => (entry.Key, entry.Value))
            .ToList();
}
