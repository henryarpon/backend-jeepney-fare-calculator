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
        var list = counts.ToList();
        var violations = FindViolations(list);
        if (violations.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", violations), nameof(counts));
        }

        _counts = list.ToDictionary(entry => entry.PassengerType, entry => entry.Count);
    }

    /// <summary>
    /// Every rule the counts break, as messages for the commuter; empty when they make a valid Party.
    /// The request validator reports these too, so the rules live only here.
    /// </summary>
    public static IReadOnlyList<string> FindViolations(IReadOnlyCollection<(PassengerType PassengerType, int Count)> counts)
    {
        var violations = new List<string>();

        var listedTwice = counts
            .GroupBy(entry => entry.PassengerType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var passengerType in listedTwice)
        {
            violations.Add($"{passengerType} is listed more than once. List each passenger type only once.");
        }

        foreach (var (passengerType, count) in counts.Where(entry => entry.Count is < 0 or > MaxPerPassengerType))
        {
            violations.Add(
                $"{passengerType} has {count} passengers. Each passenger type can have 0 to {MaxPerPassengerType} passengers.");
        }

        // Summed as long so that huge counts are reported, not overflowed.
        var total = counts.Sum(entry => (long)entry.Count);
        if (total is < MinPassengers or > MaxPassengers)
        {
            violations.Add(
                $"The party has {total} passengers in total. It must have {MinPassengers} to {MaxPassengers}.");
        }

        return violations;
    }

    /// <summary>The passenger types with at least one passenger, in <see cref="PassengerType"/> order.</summary>
    public IReadOnlyList<(PassengerType PassengerType, int Count)> Members =>
        _counts
            .Where(entry => entry.Value > 0)
            .OrderBy(entry => entry.Key)
            .Select(entry => (entry.Key, entry.Value))
            .ToList();
}
