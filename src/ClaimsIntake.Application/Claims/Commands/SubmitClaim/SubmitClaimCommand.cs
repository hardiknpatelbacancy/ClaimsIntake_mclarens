using MediatR;

namespace ClaimsIntake.Application.Claims.Commands.SubmitClaim;

public sealed record SubmitClaimCommand(
    string PolicyNumber,
    string ClaimantName,
    string Description,
    DateOnly IncidentDate) : IRequest<ClaimResponse>;
