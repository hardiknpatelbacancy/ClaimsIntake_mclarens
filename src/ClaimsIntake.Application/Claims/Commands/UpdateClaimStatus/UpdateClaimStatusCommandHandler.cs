using ClaimsIntake.Application.Abstractions;
using ClaimsIntake.Application.Common.Exceptions;
using ClaimsIntake.Domain.Entities;
using ClaimsIntake.Domain.Enums;
using MediatR;

namespace ClaimsIntake.Application.Claims.Commands.UpdateClaimStatus;

public sealed class UpdateClaimStatusCommandHandler : IRequestHandler<UpdateClaimStatusCommand, ClaimResponse>
{
    private readonly IClaimRepository _claimRepository;

    /// <summary>Creates the handler with the repository used to load and persist claims.</summary>
    public UpdateClaimStatusCommandHandler(IClaimRepository claimRepository)
    {
        _claimRepository = claimRepository;
    }

    /// <summary>
    /// Loads the claim and transitions it via the aggregate's state machine.
    /// The status string is already validated as parseable; an illegal transition throws from the entity.
    /// </summary>
    /// <exception cref="NotFoundException">No claim exists with the requested id.</exception>
    /// <exception cref="Domain.Exceptions.InvalidClaimStateTransitionException">The transition is not allowed from the claim's current status.</exception>
    public async Task<ClaimResponse> Handle(UpdateClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var claim = await _claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var newStatus = Enum.Parse<ClaimStatus>(request.NewStatus, ignoreCase: true);

        claim.TransitionTo(newStatus, request.ReviewNotes);
        await _claimRepository.SaveChangesAsync(cancellationToken);

        return ClaimResponse.FromDomain(claim);
    }
}
