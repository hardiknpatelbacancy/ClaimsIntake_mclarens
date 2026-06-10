using ClaimsIntake.Application.Abstractions;
using ClaimsIntake.Application.Common.Exceptions;
using ClaimsIntake.Domain.Entities;
using MediatR;

namespace ClaimsIntake.Application.Claims.Queries.GetClaimById;

public sealed class GetClaimByIdQueryHandler : IRequestHandler<GetClaimByIdQuery, ClaimResponse>
{
    private readonly IClaimRepository _claimRepository;

    /// <summary>Creates the handler with the repository used to look up claims.</summary>
    public GetClaimByIdQueryHandler(IClaimRepository claimRepository)
    {
        _claimRepository = claimRepository;
    }

    /// <summary>Returns the claim's read model.</summary>
    /// <exception cref="NotFoundException">No claim exists with the requested id.</exception>
    public async Task<ClaimResponse> Handle(GetClaimByIdQuery request, CancellationToken cancellationToken)
    {
        var claim = await _claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        return ClaimResponse.FromDomain(claim);
    }
}
