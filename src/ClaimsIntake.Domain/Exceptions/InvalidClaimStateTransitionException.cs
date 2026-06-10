using ClaimsIntake.Domain.Enums;

namespace ClaimsIntake.Domain.Exceptions;

public sealed class InvalidClaimStateTransitionException : DomainException
{
    public ClaimStatus From { get; }
    public ClaimStatus To { get; }

    /// <summary>Creates the exception for a disallowed transition from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public InvalidClaimStateTransitionException(ClaimStatus from, ClaimStatus to)
        : base($"A claim in status '{from}' cannot transition to '{to}'.")
    {
        From = from;
        To = to;
    }
}
