using ClaimsIntake.Domain.Entities;
using ClaimsIntake.Domain.Enums;

namespace ClaimsIntake.Application.Abstractions;

public interface IClaimRepository : IRepository<Claim>
{
    /// <summary>Returns all claims filed against the given policy number, newest first.</summary>
    Task<IReadOnlyList<Claim>> GetByPolicyNumberAsync(string policyNumber, CancellationToken cancellationToken = default);

    /// <summary>Returns all claims currently in the given status, newest first.</summary>
    Task<IReadOnlyList<Claim>> GetByStatusAsync(ClaimStatus status, CancellationToken cancellationToken = default);

    /// <summary>Returns all claims submitted within the inclusive UTC range, newest first.</summary>
    Task<IReadOnlyList<Claim>> GetSubmittedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of claims matching the optional filters (combined with AND semantics), newest first.
    /// </summary>
    Task<IReadOnlyList<Claim>> SearchAsync(ClaimStatus? status, string? policyNumber, DateTime? submittedFromUtc, DateTime? submittedToUtc, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
