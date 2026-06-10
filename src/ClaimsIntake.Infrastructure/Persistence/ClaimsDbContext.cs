using ClaimsIntake.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsIntake.Infrastructure.Persistence;

public sealed class ClaimsDbContext : DbContext
{
    /// <summary>Creates the context with externally configured options (provider, connection string).</summary>
    public ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : base(options)
    {
    }

    public DbSet<Claim> Claims => Set<Claim>();

    /// <summary>Applies every <see cref="IEntityTypeConfiguration{TEntity}"/> found in this assembly.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);
    }
}
