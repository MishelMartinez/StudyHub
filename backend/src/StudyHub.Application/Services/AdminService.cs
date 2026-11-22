using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common;
using StudyHub.Application.Import;
using StudyHub.Domain;

namespace StudyHub.Application.Services;

public record AdminQuestionDto(
    int Id, int OriginalId, int CourseId, string CourseTitle, int ModuleId, string Module, string Text, string Type,
    string OriginalState, bool HasValidationIssues, bool NeedsReview, int CurrentVersion);
public record QuestionVersionDto(int VersionNumber, int BankVersionId, DateTime CreatedAt, string ContentHash);
public record AdminQuestionDetailDto(
    AdminQuestionDto Summary, List<OptionSnapshot> Options, List<string> CorrectAnswers, string Explanation,
    string MemoryRule, string? Source, List<string> ReviewNotes, List<QuestionVersionDto> Versions);
public record PagedDto<T>(List<T> Items, int Total, int Page, int PageSize);
public record BankVersionDto(
    int Id, int CourseId, string CourseTitle, int VersionNumber, string DeclaredVersion, int? DeclaredTotal,
    string SourceFileName, string FileHash, DateTime ImportedAt, ImportReportDto? Report);

public class AdminService(IAppDbContext db)
{
    public async Task<PagedDto<AdminQuestionDto>> QuestionsAsync(int? courseId, int? moduleId, string? type, string? state,
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);
        var q = db.Questions.AsQueryable();
        if (courseId != null) q = q.Where(x => x.CourseId == courseId);
        if (moduleId != null) q = q.Where(x => x.ModuleId == moduleId);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(x => x.Type == type);
        if (state == "revision") q = q.Where(x => x.OriginalState == OriginalStates.NeedsReview || x.HasValidationIssues);
        else if (state == "validacion") q = q.Where(x => x.HasValidationIssues);
        else if (!string.IsNullOrWhiteSpace(state)) q = q.Where(x => x.OriginalState == state);
        var rows = await q.Select(x => new AdminQuestionDto(x.Id, x.OriginalId, x.CourseId, x.Course!.Title, x.ModuleId, x.Module!.Name,
            x.Text, x.Type, x.OriginalState, x.HasValidationIssues,
            x.OriginalState == OriginalStates.NeedsReview || x.HasValidationIssues, x.CurrentVersion)).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(r => r.Text.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                   r.Module.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                   r.OriginalId.ToString() == term).ToList();
        }
        rows = rows.OrderBy(r => r.CourseId).ThenBy(r => r.OriginalId).ToList();
        return new PagedDto<AdminQuestionDto>(rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(), rows.Count, page, pageSize);
    }

    public async Task<AdminQuestionDetailDto> QuestionAsync(int id, CancellationToken ct = default)
    {
        var x = await db.Questions.Include(q => q.Options).Include(q => q.Module).Include(q => q.Course)
                    .FirstOrDefaultAsync(q => q.Id == id, ct) ?? throw AppException.NotFound("Pregunta");
        var versions = await db.QuestionVersions.Where(v => v.QuestionId == id).OrderByDescending(v => v.VersionNumber)
            .Select(v => new QuestionVersionDto(v.VersionNumber, v.BankVersionId, v.CreatedAt, v.ContentHash)).ToListAsync(ct);
        var s = QuestionSnapshot.From(x, x.Module!.Name);
        return new AdminQuestionDetailDto(
            new AdminQuestionDto(x.Id, x.OriginalId, x.CourseId, x.Course!.Title, x.ModuleId, x.Module.Name, x.Text, x.Type,
                x.OriginalState, x.HasValidationIssues, x.NeedsReview, x.CurrentVersion),
            s.Options, s.CorrectAnswers, x.Explanation, x.MemoryRule, x.Source, ReviewNotes.For(x.OriginalState, s.ValidationIssues), versions);
    }

    public async Task<List<BankVersionDto>> BanksAsync(CancellationToken ct = default)
    {
        var banks = await db.BankVersions.Include(b => b.Course).OrderByDescending(b => b.Id).ToListAsync(ct);
        return banks.Select(b => new BankVersionDto(b.Id, b.CourseId, b.Course!.Title, b.VersionNumber, b.DeclaredVersion,
            b.DeclaredTotal, b.SourceFileName, b.FileHash, b.ImportedAt,
            string.IsNullOrEmpty(b.ReportJson) ? null : Json.Deserialize<ImportReportDto>(b.ReportJson))).ToList();
    }

    public async Task<(string FileName, string Content)> BankFileAsync(int id, CancellationToken ct = default)
    {
        var b = await db.BankVersions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Versión del banco");
        return (b.SourceFileName, b.RawContent);
    }
}
