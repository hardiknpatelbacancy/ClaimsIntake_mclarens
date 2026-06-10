using ClaimsIntake.Application.Abstractions;
using ClaimsIntake.Domain.Enums;
using MediatR;

namespace ClaimsIntake.Application.Claims.Queries.GetClaims;

public sealed class GetClaimsQueryHandler : IRequestHandler<GetClaimsQuery, IReadOnlyList<ClaimResponse>>
{
    private readonly IClaimRepository _claimRepository;

    /// <summary>Creates the handler with the repository used to search claims.</summary>
    public GetClaimsQueryHandler(IClaimRepository claimRepository)
    {
        _claimRepository = claimRepository;
    }

    /// <summary>
    /// Returns the matching page of claim read models. The status filter is already
    /// validated as parseable by <see cref="GetClaimsQueryValidator"/>.
    /// </summary>
    public async Task<IReadOnlyList<ClaimResponse>> Handle(GetClaimsQuery request, CancellationToken cancellationToken)
    {
        ClaimStatus? status = string.IsNullOrWhiteSpace(request.Status)
            ? null
            : Enum.Parse<ClaimStatus>(request.Status, ignoreCase: true);

        var claims = await _claimRepository.SearchAsync(
            status,
            request.PolicyNumber,
            request.SubmittedFrom,
            request.SubmittedTo,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return claims.Select(ClaimResponse.FromDomain).ToList();
    }
}
