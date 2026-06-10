using ClaimsIntake.Application.Abstractions;
using ClaimsIntake.Domain.Entities;
using MediatR;

namespace ClaimsIntake.Application.Claims.Commands.SubmitClaim;

public sealed class SubmitClaimCommandHandler : IRequestHandler<SubmitClaimCommand, ClaimResponse>
{
    private readonly IClaimRepository _claimRepository;

    /// <summary>Creates the handler with the repository used to persist new claims.</summary>
    public SubmitClaimCommandHandler(IClaimRepository claimRepository)
    {
        _claimRepository = claimRepository;
    }

    /// <summary>Creates a claim via the <see cref="Claim.Submit"/> factory, persists it, and returns its read model.</summary>
    public async Task<ClaimResponse> Handle(SubmitClaimCommand request, CancellationToken cancellationToken)
    {
        var claim = Claim.Submit(
            request.PolicyNumber,
            request.ClaimantName,
            request.Description,
            request.IncidentDate);

        await _claimRepository.AddAsync(claim, cancellationToken);
        await _claimRepository.SaveChangesAsync(cancellationToken);

        return ClaimResponse.FromDomain(claim);
    }
}
