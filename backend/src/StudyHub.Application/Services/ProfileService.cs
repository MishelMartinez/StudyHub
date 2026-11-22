using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common;
using StudyHub.Domain;

namespace StudyHub.Application.Services;

public record ProfileDto(int Id, string Name, int PassThreshold);
public record ProfileRequest(string? Name, int? PassThreshold);

public class ProfileService(IAppDbContext db)
{
    public async Task<List<ProfileDto>> ListAsync(CancellationToken ct = default) =>
        await db.Profiles.OrderBy(p => p.Id).Select(p => new ProfileDto(p.Id, p.Name, p.PassThreshold)).ToListAsync(ct);

    public async Task<ProfileDto> CreateAsync(ProfileRequest request, CancellationToken ct = default)
    {
        var name = ValidName(request.Name);
        var p = new UserProfile { Name = name, PassThreshold = ValidThreshold(request.PassThreshold ?? 70), CreatedAt = DateTime.UtcNow };
        db.Profiles.Add(p);
        await db.SaveChangesAsync(ct);
        return new ProfileDto(p.Id, p.Name, p.PassThreshold);
    }

    public async Task<ProfileDto> UpdateAsync(int id, ProfileRequest request, CancellationToken ct = default)
    {
        var p = await db.Profiles.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Perfil");
        if (request.Name != null) p.Name = ValidName(request.Name);
        if (request.PassThreshold != null) p.PassThreshold = ValidThreshold(request.PassThreshold.Value);
        await db.SaveChangesAsync(ct);
        return new ProfileDto(p.Id, p.Name, p.PassThreshold);
    }

    private static string ValidName(string? name)
    {
        var n = name?.Trim() ?? "";
        if (n.Length is < 1 or > 60) throw AppException.Invalid("El nombre del perfil debe tener entre 1 y 60 caracteres.");
        return n;
    }

    private static int ValidThreshold(int value) =>
        value is >= 1 and <= 100 ? value : throw AppException.Invalid("El umbral de aprobación debe estar entre 1 y 100.");
}
