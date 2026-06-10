using ClaimsIntake.Application.Abstractions;
using ClaimsIntake.Domain.Entities;
using ClaimsIntake.Domain.Enums;

namespace ClaimsIntake.Tests.Application.Fakes;

/// <summary>In-memory IClaimRepository; records SearchAsync arguments for assertion.</summary>
public sealed class FakeClaimRepository : IClaimRepository
{
    public List<Claim> Claims { get; } = [];
    public int SaveChangesCalls { get; private set; }

    public (ClaimStatus? Status, string? PolicyNumber, DateTime? FromUtc, DateTime? ToUtc, int PageNumber, int PageSize)? LastSearch
    { get; private set; }

    public Task<Claim?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Claims.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Claim>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Claim>>(Claims.ToList());

    public Task AddAsync(Claim entity, CancellationToken cancellationToken = default)
    {
        Claims.Add(entity);
        return Task.CompletedTask;
    }

    public void Remove(Claim entity) => Claims.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }

    public Task<IReadOnlyList<Claim>> GetByPolicyNumberAsync(string policyNumber, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Claim>>(Claims.Where(c => c.PolicyNumber == policyNumber).ToList());

    public Task<IReadOnlyList<Claim>> GetByStatusAsync(ClaimStatus status, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Claim>>(Claims.Where(c => c.Status == status).ToList());

    public Task<IReadOnlyList<Claim>> GetSubmittedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Claim>>(Claims.Where(c => c.SubmittedAt >= fromUtc && c.SubmittedAt <= toUtc).ToList());

    public Task<IReadOnlyList<Claim>> SearchAsync(
        ClaimStatus? status,
        string? policyNumber,
        DateTime? submittedFromUtc,
        DateTime? submittedToUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        LastSearch = (status, policyNumber, submittedFromUtc, submittedToUtc, pageNumber, pageSize);

        var result = Claims
            .Where(c => !status.HasValue || c.Status == status.Value)
            .Where(c => string.IsNullOrWhiteSpace(policyNumber) || c.PolicyNumber == policyNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<Claim>>(result);
    }
}
