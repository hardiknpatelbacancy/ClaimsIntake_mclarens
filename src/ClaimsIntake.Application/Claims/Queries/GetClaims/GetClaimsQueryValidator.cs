using ClaimsIntake.Domain.Enums;
using FluentValidation;

namespace ClaimsIntake.Application.Claims.Queries.GetClaims;

public sealed class GetClaimsQueryValidator : AbstractValidator<GetClaimsQuery>
{
    /// <summary>
    /// Defines the list-query rules: a parseable status filter (when present), a coherent
    /// submitted-date range, and paging within bounds (page size 1–100).
    /// </summary>
    public GetClaimsQueryValidator()
    {
        RuleFor(q => q.Status)
            .Must(status => Enum.TryParse<ClaimStatus>(status, ignoreCase: true, out _))
            .When(q => !string.IsNullOrWhiteSpace(q.Status))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<ClaimStatus>())}.");

        RuleFor(q => q.PolicyNumber)
            .MaximumLength(50);

        RuleFor(q => q.SubmittedFrom)
            .LessThanOrEqualTo(q => q.SubmittedTo)
            .When(q => q.SubmittedFrom.HasValue && q.SubmittedTo.HasValue)
            .WithMessage("'SubmittedFrom' must be earlier than or equal to 'SubmittedTo'.");

        RuleFor(q => q.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100);
    }
}
