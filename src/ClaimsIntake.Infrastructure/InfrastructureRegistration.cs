using ClaimsIntake.Application.Abstractions;
using ClaimsIntake.Domain.Entities;
using ClaimsIntake.Infrastructure.Persistence;
using ClaimsIntake.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsIntake.Infrastructure;

public static class InfrastructureRegistration
{
    /// <summary>
    /// Registers the SQL Server <see cref="ClaimsDbContext"/> and the claim repository, wired so
    /// <see cref="IClaimRepository"/> and <see cref="IRepository{T}"/> resolve to the same scoped instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">The 'ClaimsIntake' connection string is missing.</exception>
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ClaimsIntake")
            ?? throw new InvalidOperationException("Connection string 'ClaimsIntake' is not configured.");

        services.AddDbContext<ClaimsDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IRepository<Claim>>(sp => sp.GetRequiredService<IClaimRepository>());

        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations. Intended for development startup only —
    /// production schema changes should run through a deployment pipeline.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
