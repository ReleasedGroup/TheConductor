using Microsoft.EntityFrameworkCore;

namespace Conductor.Infrastructure.Persistence.Sqlite;

public sealed class ConductorDbContext(DbContextOptions<ConductorDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 64);
        modelBuilder.ApplyInitialSchema();
    }
}
