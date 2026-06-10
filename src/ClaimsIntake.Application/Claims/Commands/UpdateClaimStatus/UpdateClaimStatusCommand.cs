using MediatR;

namespace ClaimsIntake.Application.Claims.Commands.UpdateClaimStatus;

/// <summary>
/// <paramref name="NewStatus"/> is the target <see cref="Domain.Enums.ClaimStatus"/> name.
/// Whether the value parses is validation (422); whether the transition is legal is a
/// domain decision (409).
/// </summary>
public sealed record UpdateClaimStatusCommand(
    Guid ClaimId,
    string NewStatus,
    string? ReviewNotes) : IRequest<ClaimResponse>;
