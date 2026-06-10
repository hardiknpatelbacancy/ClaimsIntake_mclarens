using ClaimsIntake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClaimsIntake.Tests.Integration;

/// <summary>
/// Test host running the real HTTP pipeline against SQLite in-memory.
/// Environment is "Testing" so the Development-only SQL Server startup
/// migration never runs; the schema is created with EnsureCreated because
/// the checked-in migrations are SQL Server dialect.
/// </summary>
public sealed class ClaimsApiFactory : WebApplicationFactory<Program>
{
    // A single open connection keeps the in-memory database alive for the factory's lifetime.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private bool _databaseCreated;
    private readonly Lock _lock = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Swap the SQL Server DbContext registered by RegisterInfrastructure for SQLite.
            services.RemoveAll(typeof(DbContextOptions<ClaimsDbContext>));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ClaimsDbContext>));
            services.RemoveAll(typeof(ClaimsDbContext));
            services.AddDbContext<ClaimsDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public void EnsureDatabaseCreated()
    {
        lock (_lock)
        {
            if (_databaseCreated)
                return;

            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Database.EnsureCreated();
            _databaseCreated = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
