using MediatR;

namespace ClaimsIntake.Application.Claims.Queries.GetClaimById;

public sealed record GetClaimByIdQuery(Guid ClaimId) : IRequest<ClaimResponse>;
