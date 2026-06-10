using ClaimsIntake.Domain.Enums;
using FluentValidation;

namespace ClaimsIntake.Application.Claims.Commands.UpdateClaimStatus;

public sealed class UpdateClaimStatusCommandValidator : AbstractValidator<UpdateClaimStatusCommand>
{
    /// <summary>
    /// Defines the transition-request rules: the status must parse to a <see cref="ClaimStatus"/>
    /// (case-insensitive), and review notes are mandatory when denying or requesting more information.
    /// </summary>
    public UpdateClaimStatusCommandValidator()
    {
        RuleFor(c => c.ClaimId)
            .NotEmpty();

        RuleFor(c => c.NewStatus)
            .NotEmpty()
            .Must(status => Enum.TryParse<ClaimStatus>(status, ignoreCase: true, out _))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<ClaimStatus>())}.");

        RuleFor(c => c.ReviewNotes)
            .MaximumLength(4000);

        // Denying a claim or asking for more information without an explanation
        // is useless to the claimant, so notes are mandatory for those targets.
        RuleFor(c => c.ReviewNotes)
            .NotEmpty()
            .When(c => RequiresReviewNotes(c.NewStatus))
            .WithMessage("Review notes are required when denying a claim or requesting additional information.");
    }

    /// <summary>True when the target status is one that demands an explanation for the claimant.</summary>
    private static bool RequiresReviewNotes(string newStatus) =>
        Enum.TryParse<ClaimStatus>(newStatus, ignoreCase: true, out var status)
        && status is ClaimStatus.Denied or ClaimStatus.AdditionalInfoRequired;
}
