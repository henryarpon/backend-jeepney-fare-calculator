using FluentValidation;
using JeepneyFareCalculator.Domain.Fares;

namespace JeepneyFareCalculator.Application.Fares;

/// <summary>
/// Checks a fare calculation request before any distance is estimated, so a bad
/// request never uses up routing quota. A failure is answered with 400.
/// </summary>
public sealed class CalculateFareRequestValidator : AbstractValidator<CalculateFareRequest>
{
    public CalculateFareRequestValidator()
    {
        RuleFor(request => request.VehicleType)
            .NotNull().WithMessage("Choose a vehicle type.")
            .IsInEnum().WithMessage("Choose Traditional Jeepney or Modern Jeepney.");

        RuleFor(request => request.Origin)
            .NotNull().WithMessage("Choose an origin.")
            .SetValidator(new CoordinatesValidator()!);

        RuleFor(request => request.Destination)
            .NotNull().WithMessage("Choose a destination.")
            .SetValidator(new CoordinatesValidator()!);

        RuleFor(request => request.Party)
            .NotNull().WithMessage("Add at least one passenger.");

        RuleForEach(request => request.Party)
            .NotNull().WithMessage("A party entry is missing.")
            .SetValidator(new PartyMemberValidator());

        RuleFor(request => request.Party)
            .Must(party => party!.Select(member => member?.PassengerType).Distinct().Count() == party!.Count)
            .WithMessage("List each passenger type only once.")
            .Must(party => party!.Sum(member => member?.Count ?? 0) is >= Party.MinPassengers and <= Party.MaxPassengers)
            .WithMessage($"The party must have {Party.MinPassengers} to {Party.MaxPassengers} passengers in total.")
            .When(request => request.Party is not null);
    }

    private sealed class CoordinatesValidator : AbstractValidator<CoordinatesDto>
    {
        public CoordinatesValidator()
        {
            RuleFor(point => point.Lat)
                .NotNull().WithMessage("The latitude is missing.")
                .InclusiveBetween(-90, 90).WithMessage("The latitude must be between -90 and 90.");

            RuleFor(point => point.Lng)
                .NotNull().WithMessage("The longitude is missing.")
                .InclusiveBetween(-180, 180).WithMessage("The longitude must be between -180 and 180.");
        }
    }

    private sealed class PartyMemberValidator : AbstractValidator<PartyMemberDto>
    {
        public PartyMemberValidator()
        {
            RuleFor(member => member.PassengerType)
                .NotNull().WithMessage("The passenger type is missing.")
                .IsInEnum().WithMessage("The passenger type must be Regular, Student, SeniorCitizen or Pwd.");

            RuleFor(member => member.Count)
                .NotNull().WithMessage("The count is missing.")
                .InclusiveBetween(0, Party.MaxPerPassengerType)
                .WithMessage($"Each passenger type can have 0 to {Party.MaxPerPassengerType} passengers.");
        }
    }
}
