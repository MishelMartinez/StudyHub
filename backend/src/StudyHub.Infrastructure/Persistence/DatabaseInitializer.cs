using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyHub.Application.Import;
using StudyHub.Domain;

namespace StudyHub.Infrastructure.Persistence;

/// <summary>Applies migrations, ensures the default profile and imports the seed bank on first run.</summary>
public class DatabaseInitializer(AppDbContext db, ImportService import, IConfiguration config, IHostEnvironment env, ILogger<DatabaseInitializer> log)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        if (!await db.Profiles.AnyAsync(ct))
        {
            db.Profiles.Add(new UserProfile { Name = "Perfil principal", PassThreshold = 70, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
        }
        if (await db.BankVersions.AnyAsync(ct)) return;

        var seedPath = config["Seed:BankPath"];
        if (string.IsNullOrWhiteSpace(seedPath)) return;
        var full = Path.IsPathRooted(seedPath) ? seedPath : Path.GetFullPath(Path.Combine(env.ContentRootPath, seedPath));
        if (!File.Exists(full))
        {
            log.LogWarning("Banco semilla no encontrado en {Path}", full);
            return;
        }
        var report = await import.CommitAsync(await File.ReadAllTextAsync(full, ct), Path.GetFileName(full), ct);
        log.LogInformation("Banco semilla importado: {Status}, {Added} preguntas, {Modules} módulos", report.Status, report.Added, report.ModuleCount);
    }
}
