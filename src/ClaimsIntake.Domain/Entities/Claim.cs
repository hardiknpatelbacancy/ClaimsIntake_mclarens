using ClaimsIntake.Domain.Enums;
using ClaimsIntake.Domain.Exceptions;

namespace ClaimsIntake.Domain.Entities;

public sealed class Claim
{
    // The state machine lives here so an invalid transition can never be persisted.
    private static readonly IReadOnlyDictionary<ClaimStatus, ClaimStatus[]> AllowedTransitions =
        new Dictionary<ClaimStatus, ClaimStatus[]>
        {
            [ClaimStatus.Submitted] = [ClaimStatus.UnderReview],
            [ClaimStatus.UnderReview] = [ClaimStatus.AdditionalInfoRequired, ClaimStatus.Approved, ClaimStatus.Denied],
            [ClaimStatus.AdditionalInfoRequired] = [ClaimStatus.UnderReview],
            [ClaimStatus.Approved] = [],
            [ClaimStatus.Denied] = []
        };

    public Guid Id { get; private set; }
    public string PolicyNumber { get; private set; } = null!;
    public string ClaimantName { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public DateOnly IncidentDate { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }
    public ClaimStatus Status { get; private set; }
    public string? ReviewNotes { get; private set; }

    /// <summary>Reserved for EF Core materialization; use <see cref="Submit"/> to create claims.</summary>
    private Claim()
    {
    }

    /// <summary>
    /// Creates a new claim in the <see cref="ClaimStatus.Submitted"/> status, trimming inputs and
    /// stamping <see cref="SubmittedAt"/>/<see cref="LastUpdatedAt"/> with the current UTC time.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A required value is missing/whitespace, or <paramref name="incidentDate"/> is in the future.
    /// </exception>
    public static Claim Submit(string policyNumber, string claimantName, string description, DateOnly incidentDate)
    {
        if (string.IsNullOrWhiteSpace(policyNumber))
            throw new ArgumentException("Policy number is required.", nameof(policyNumber));
        if (string.IsNullOrWhiteSpace(claimantName))
            throw new ArgumentException("Claimant name is required.", nameof(claimantName));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        var now = DateTime.UtcNow;
        if (incidentDate > DateOnly.FromDateTime(now))
            throw new ArgumentException("Incident date cannot be in the future.", nameof(incidentDate));

        return new Claim
        {
            Id = Guid.NewGuid(),
            PolicyNumber = policyNumber.Trim(),
            ClaimantName = claimantName.Trim(),
            Description = description.Trim(),
            IncidentDate = incidentDate,
            SubmittedAt = now,
            LastUpdatedAt = now,
            Status = ClaimStatus.Submitted
        };
    }

    /// <summary>
    /// Moves the claim to <paramref name="newStatus"/> if the state machine allows it,
    /// updating <see cref="LastUpdatedAt"/> and overwriting <see cref="ReviewNotes"/> when notes are provided.
    /// </summary>
    /// <exception cref="InvalidClaimStateTransitionException">The transition is not allowed from the current status.</exception>
    public void TransitionTo(ClaimStatus newStatus, string? reviewNotes = null)
    {
        if (!AllowedTransitions[Status].Contains(newStatus))
            throw new InvalidClaimStateTransitionException(Status, newStatus);

        Status = newStatus;
        LastUpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(reviewNotes))
            ReviewNotes = reviewNotes.Trim();
    }
}
