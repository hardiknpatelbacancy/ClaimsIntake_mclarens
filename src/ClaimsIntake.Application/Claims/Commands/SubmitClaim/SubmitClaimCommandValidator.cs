using FluentValidation;

namespace ClaimsIntake.Application.Claims.Commands.SubmitClaim;

public sealed class SubmitClaimCommandValidator : AbstractValidator<SubmitClaimCommand>
{
    /// <summary>
    /// Defines the submission rules: policy number format (<c>POL-</c> + 4–10 digits), required
    /// name/description with length caps, and an incident date within the past two years.
    /// </summary>
    public SubmitClaimCommandValidator()
    {
        RuleFor(c => c.PolicyNumber)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(@"^POL-\d{4,10}$")
            .WithMessage("Policy number must match the format 'POL-' followed by 4 to 10 digits (e.g. POL-1001).");

        RuleFor(c => c.ClaimantName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(c => c.Description)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(c => c.IncidentDate)
            .NotEmpty()
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Incident date cannot be in the future.")
            .Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2))
            .WithMessage("Incident date cannot be more than 2 years in the past.");
    }
}
