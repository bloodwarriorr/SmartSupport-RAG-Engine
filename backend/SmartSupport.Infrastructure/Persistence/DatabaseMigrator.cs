using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SmartSupport.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations when the app starts, so a deploy updates the database by itself.
/// </summary>
public static class DatabaseMigrator
{
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrator));

        // If the original tables were created without EF recording it (e.g. copied between databases),
        // mark the first migration as applied so EF doesn't try to create those tables again.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
                "ProductVersion" character varying(32) NOT NULL
            );
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260415143356_InitialSupabaseCreate', '9.0.0'
            WHERE to_regclass('public."Users"') IS NOT NULL
            ON CONFLICT ("MigrationId") DO NOTHING;
            """, cancellationToken);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying database migrations: {Migrations}", string.Join(", ", pending));
            await db.Database.MigrateAsync(cancellationToken);
        }
        logger.LogInformation("Database is up to date.");
    }
}
