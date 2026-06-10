using ClaimsIntake.Application.Abstractions;
using ClaimsIntake.Domain.Entities;
using ClaimsIntake.Domain.Enums;
using ClaimsIntake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsIntake.Infrastructure.Repositories;

public sealed class ClaimRepository : IClaimRepository
{
    private readonly ClaimsDbContext _dbContext;

    /// <summary>Creates the repository over the given EF Core context.</summary>
    public ClaimRepository(ClaimsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<Claim?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.Claims.FindAsync([id], cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Claim>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Claims
            .AsNoTracking()
            .OrderByDescending(c => c.SubmittedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task AddAsync(Claim entity, CancellationToken cancellationToken = default)
        => await _dbContext.Claims.AddAsync(entity, cancellationToken);

    /// <inheritdoc/>
    public void Remove(Claim entity)
        => _dbContext.Claims.Remove(entity);

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Claim>> GetByPolicyNumberAsync(string policyNumber, CancellationToken cancellationToken = default)
        => await _dbContext.Claims
            .AsNoTracking()
            .Where(c => c.PolicyNumber == policyNumber)
            .OrderByDescending(c => c.SubmittedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Claim>> GetByStatusAsync(ClaimStatus status, CancellationToken cancellationToken = default)
        => await _dbContext.Claims
            .AsNoTracking()
            .Where(c => c.Status == status)
            .OrderByDescending(c => c.SubmittedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Claim>> GetSubmittedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => await _dbContext.Claims
            .AsNoTracking()
            .Where(c => c.SubmittedAt >= fromUtc && c.SubmittedAt <= toUtc)
            .OrderByDescending(c => c.SubmittedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Claim>> SearchAsync(
        ClaimStatus? status,
        string? policyNumber,
        DateTime? submittedFromUtc,
        DateTime? submittedToUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Claims.AsNoTracking();

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(policyNumber))
            query = query.Where(c => c.PolicyNumber == policyNumber);

        if (submittedFromUtc.HasValue)
            query = query.Where(c => c.SubmittedAt >= submittedFromUtc.Value);

        if (submittedToUtc.HasValue)
            query = query.Where(c => c.SubmittedAt <= submittedToUtc.Value);

        return await query
            .OrderByDescending(c => c.SubmittedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
}
