using ClaimsIntake.Domain.Entities;

namespace ClaimsIntake.Application.Claims;

/// <summary>Read model returned by all claim endpoints.</summary>
public sealed record ClaimResponse(
    Guid Id,
    string PolicyNumber,
    string ClaimantName,
    string Description,
    DateOnly IncidentDate,
    DateTime SubmittedAt,
    DateTime LastUpdatedAt,
    string Status,
    string? ReviewNotes)
{
    /// <summary>Maps a <see cref="Claim"/> aggregate to the response shape, rendering the status as its enum name.</summary>
    public static ClaimResponse FromDomain(Claim claim) => new(
        claim.Id,
        claim.PolicyNumber,
        claim.ClaimantName,
        claim.Description,
        claim.IncidentDate,
        claim.SubmittedAt,
        claim.LastUpdatedAt,
        claim.Status.ToString(),
        claim.ReviewNotes);
}
