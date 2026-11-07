namespace JeepneyFareCalculator.Domain.Fares;

/// <summary>
/// The fare category of a passenger. Every type except <see cref="Regular"/> pays
/// the Discounted Fare. The order here is the order a breakdown lists them in.
/// </summary>
public enum PassengerType
{
    Regular,
    Student,
    SeniorCitizen,
    Pwd,
}
