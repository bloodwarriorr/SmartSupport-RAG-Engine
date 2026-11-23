using Microsoft.EntityFrameworkCore;

namespace SmartSupport.Api.Data;

/// <summary>
/// Applies pending EF Core migrations when the app starts, so a deploy updates the database by itself.
/// </summary>
public static class DatabaseMigrator
{
    public static void ApplyMigrations(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // If the original tables were created without EF recording it (e.g. copied between databases),
            // mark the first migration as applied so EF doesn't try to create those tables again.
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
                    "ProductVersion" character varying(32) NOT NULL
                );
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT '20260415143356_InitialSupabaseCreate', '9.0.0'
                WHERE to_regclass('public."Users"') IS NOT NULL
                ON CONFLICT ("MigrationId") DO NOTHING;
                """);

            var pending = db.Database.GetPendingMigrations().ToList();
            if (pending.Count > 0)
            {
                Console.WriteLine($"--- Applying database migrations: {string.Join(", ", pending)}");
                db.Database.Migrate();
            }
            Console.WriteLine("--- Database is up to date.");
        }
        catch (Exception ex)
        {
            // Keep the app running so the logs are reachable, but make the failure obvious.
            Console.WriteLine($"--- DATABASE MIGRATION FAILED: {ex.Message}");
        }
    }
}
