using Conductor.Core.Application.Queries;
using Conductor.Core.Domain;
using Conductor.Infrastructure.Persistence.Sqlite;
using Conductor.Infrastructure.Persistence.Sqlite.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Persistence.Tests;

public sealed class SqlitePersistenceSmokeTests
{
    [Fact]
    public async Task DbContext_Can_Create_InMemory_Sqlite_Database()
    {
        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using ConductorDbContext dbContext = new(options);

        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        Assert.True(await dbContext.Database.CanConnectAsync());
    }

    [Fact]
    public async Task DevelopmentSeedData_Is_Idempotent_And_Populates_Dashboard_Data()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var dbContext = new ConductorDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
            await DevelopmentSeedData.SeedAsync(dbContext);
            await DevelopmentSeedData.SeedAsync(dbContext);
        }

        await using var assertionContext = new ConductorDbContext(options);

        Assert.Equal(2, await assertionContext.Projects.CountAsync());
        Assert.Equal(3, await assertionContext.Repositories.CountAsync());
        Assert.Equal(3, await assertionContext.SymphonyInstances.CountAsync());
        Assert.Equal(3, await assertionContext.InstanceSnapshots.CountAsync());
    }

    [Fact]
    public async Task ReadModelQueries_Return_Seeded_Dashboard_Projection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ConductorDbContext> options = new DbContextOptionsBuilder<ConductorDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var dbContext = new ConductorDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
            await DevelopmentSeedData.SeedAsync(dbContext);
        }

        var queries = new EfConductorReadModelQueries(new TestDbContextFactory(options));

        ConductorDashboardSummary summary = await queries.GetDashboardSummaryAsync();

        Assert.Equal(2, summary.ProjectCount);
        Assert.Equal(3, summary.RepositoryCount);
        Assert.Equal(3, summary.InstanceCount);
        Assert.Equal(2, summary.NeedsAttentionCount);
        Assert.Contains(summary.ActiveRepositories, repository => repository.FullName == "ReleasedGroup/TheConductor");
        Assert.Contains(summary.NeedsAttention, repository => repository.HealthStatus == InstanceHealthStatus.Offline);
    }

    private sealed class TestDbContextFactory(DbContextOptions<ConductorDbContext> options)
        : IDbContextFactory<ConductorDbContext>
    {
        public ConductorDbContext CreateDbContext() => new(options);
    }
}
