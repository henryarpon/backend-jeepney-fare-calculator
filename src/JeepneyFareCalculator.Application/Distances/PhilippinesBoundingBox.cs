namespace JeepneyFareCalculator.Application.Distances;

/// <summary>
/// A rough rectangle around the Philippines, from Tawi-Tawi in the south to Batanes
/// in the north. It is meant to catch obvious mistakes, such as a same-named place
/// abroad, not to give exact borders: it also takes in a little sea and some of
/// northern Borneo.
/// </summary>
/// <remarks>The front end limits place search to the same box (Photon's <c>bbox</c>).</remarks>
public static class PhilippinesBoundingBox
{
    public const double MinLatitude = 4.5;
    public const double MaxLatitude = 21.2;
    public const double MinLongitude = 116.9;
    public const double MaxLongitude = 126.7;

    public static bool Contains(Coordinates point) =>
        point.Latitude is >= MinLatitude and <= MaxLatitude &&
        point.Longitude is >= MinLongitude and <= MaxLongitude;
}
