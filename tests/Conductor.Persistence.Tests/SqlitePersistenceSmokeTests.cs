using Conductor.Infrastructure.Persistence.Sqlite;
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
}
