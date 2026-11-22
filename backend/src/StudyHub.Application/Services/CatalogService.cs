using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common;
using StudyHub.Domain;

namespace StudyHub.Application.Services;

public record ModuleStatsDto(
    int Id, int CourseId, string Name, int SortOrder, int Total, int InReview, int Attempted, int CorrectLast,
    int Mastered, int WithErrors, double ProgressPercentage);
public record CourseStatsDto(
    int Id, string Title, int ModuleCount, int Total, int InReview, int Attempted, int CorrectLast, int Mastered,
    int WithErrors, double ProgressPercentage, int BankVersions);
public record ActivityDto(DateTime At, string Context, int QuestionId, int OriginalId, string Module, string Text, bool IsCorrect);
public record ContinueStudyDto(int CourseId, int ModuleId, string ModuleName);
public record DashboardDto(
    List<CourseStatsDto> Courses, int TotalAttempts, int CorrectAttempts, double Accuracy,
    List<ActivityDto> RecentActivity, List<ExamSummaryDto> RecentExams, ContinueStudyDto? ContinueStudy, int PassThreshold);
public record RuleDto(int QuestionId, int OriginalId, string Rule, bool NeedsReview);
public record RuleGroupDto(int ModuleId, string Module, List<RuleDto> Rules);
public record ErrorEntryDto(
    int QuestionId, int OriginalId, int CourseId, int ModuleId, string Module, string Text, int Attempts, int Errors,
    List<string> LastSelected, List<string> CorrectAnswers, DateTime LastAttemptAt, DateTime LastErrorAt,
    string Mastery, string MemoryRule, bool NeedsReview);
public record ErrorModuleDto(int ModuleId, string Module, int Questions, int Errors);
public record ErrorLogDto(List<ErrorEntryDto> Entries, List<ErrorModuleDto> ByModule);

public class CatalogService(IAppDbContext db, ICurrentProfile profile, ProgressReader progress, ExamService exams)
{
    public async Task<List<CourseStatsDto>> CoursesAsync(CancellationToken ct = default)
    {
        var courses = await db.Courses.OrderBy(c => c.Title).ToListAsync(ct);
        var result = new List<CourseStatsDto>();
        foreach (var c in courses)
        {
            var modules = await ModulesAsync(c.Id, ct);
            var total = modules.Sum(m => m.Total);
            var attempted = modules.Sum(m => m.Attempted);
            result.Add(new CourseStatsDto(c.Id, c.Title, modules.Count, total, modules.Sum(m => m.InReview), attempted,
                modules.Sum(m => m.CorrectLast), modules.Sum(m => m.Mastered), modules.Sum(m => m.WithErrors),
                total == 0 ? 0 : Math.Round(attempted * 100.0 / total, 1),
                await db.BankVersions.CountAsync(b => b.CourseId == c.Id, ct)));
        }
        return result;
    }

    public async Task<List<ModuleStatsDto>> ModulesAsync(int courseId, CancellationToken ct = default)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == courseId, ct)) throw AppException.NotFound("Curso");
        var scope = db.Questions.Where(q => q.CourseId == courseId);
        var snap = await progress.LoadAsync(scope, ct);
        var questions = await scope.Select(q => new { q.Id, q.ModuleId, q.OriginalState, q.HasValidationIssues }).ToListAsync(ct);
        var modules = await db.Modules.Where(m => m.CourseId == courseId).OrderBy(m => m.SortOrder).ToListAsync(ct);
        return modules.Select(m =>
        {
            var qs = questions.Where(q => q.ModuleId == m.Id).ToList();
            var prog = qs.Select(q => snap.For(q.Id)).ToList();
            var attempted = prog.Count(p => p.Attempts > 0);
            return new ModuleStatsDto(m.Id, courseId, m.Name, m.SortOrder, qs.Count,
                qs.Count(q => q.OriginalState == OriginalStates.NeedsReview || q.HasValidationIssues), attempted,
                prog.Count(p => p.LastCorrect == true), prog.Count(p => p.Mastery == Mastery.Mastered),
                prog.Count(p => p.Mastery == Mastery.WithErrors),
                qs.Count == 0 ? 0 : Math.Round(attempted * 100.0 / qs.Count, 1));
        }).Where(m => m.Total > 0).ToList();
    }

    public async Task<DashboardDto> DashboardAsync(CancellationToken ct = default)
    {
        var courses = await CoursesAsync(ct);
        var attempts = db.Attempts.Where(a => a.ProfileId == profile.ProfileId);
        var total = await attempts.CountAsync(ct);
        var correct = await attempts.CountAsync(a => a.IsCorrect, ct);
        var recent = await attempts.OrderByDescending(a => a.Id).Take(8)
            .Select(a => new ActivityDto(a.CreatedAt, a.Context, a.QuestionId, a.Question!.OriginalId, a.Question.Module!.Name, a.Question.Text, a.IsCorrect))
            .ToListAsync(ct);
        var last = await attempts.Where(a => a.Context == AttemptContexts.Study).OrderByDescending(a => a.Id)
            .Select(a => new ContinueStudyDto(a.Question!.CourseId, a.Question.ModuleId, a.Question.Module!.Name)).FirstOrDefaultAsync(ct);
        var threshold = await db.Profiles.Where(p => p.Id == profile.ProfileId).Select(p => p.PassThreshold).FirstAsync(ct);
        var examList = (await exams.ListAsync(ct)).Take(5).ToList();
        return new DashboardDto(courses, total, correct, total == 0 ? 0 : Math.Round(correct * 100.0 / total, 1),
            recent, examList, last, threshold);
    }

    public async Task<List<RuleGroupDto>> RulesAsync(int? courseId, int? moduleId, string? search, CancellationToken ct = default)
    {
        var q = db.Questions.Include(x => x.Module).Where(x => x.MemoryRule != "");
        if (courseId != null) q = q.Where(x => x.CourseId == courseId);
        if (moduleId != null) q = q.Where(x => x.ModuleId == moduleId);
        var list = await q.ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            list = list.Where(x => x.MemoryRule.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                   x.Module!.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return list.GroupBy(x => x.Module!).OrderBy(g => g.Key.SortOrder)
            .Select(g => new RuleGroupDto(g.Key.Id, g.Key.Name,
                g.OrderBy(x => x.OriginalId).Select(x => new RuleDto(x.Id, x.OriginalId, x.MemoryRule, x.NeedsReview)).ToList()))
            .ToList();
    }

    public async Task<ErrorLogDto> ErrorLogAsync(int? courseId, CancellationToken ct = default)
    {
        var scope = db.Questions.AsQueryable();
        if (courseId != null) scope = scope.Where(q => q.CourseId == courseId);
        var snap = await progress.LoadAsync(scope, ct);
        var withErrors = snap.Attempts.Where(kv => kv.Value.Any(a => !a.IsCorrect)).Select(kv => kv.Key).ToList();
        var questions = await db.Questions.Include(q => q.Module).Where(q => withErrors.Contains(q.Id)).ToListAsync(ct);
        var entries = questions.Select(q =>
        {
            var list = snap.Attempts[q.Id];
            var wrong = list.Where(a => !a.IsCorrect).ToList();
            return new ErrorEntryDto(q.Id, q.OriginalId, q.CourseId, q.ModuleId, q.Module!.Name, q.Text, list.Count, wrong.Count,
                Json.Keys(wrong[^1].SelectedJson), Json.Keys(q.CorrectAnswersJson), list[^1].CreatedAt, wrong[^1].CreatedAt,
                snap.For(q.Id).Mastery, q.MemoryRule, q.NeedsReview);
        }).OrderByDescending(e => e.Mastery == Mastery.WithErrors).ThenByDescending(e => e.Errors).ThenByDescending(e => e.LastErrorAt).ToList();
        var byModule = entries.GroupBy(e => (e.ModuleId, e.Module))
            .Select(g => new ErrorModuleDto(g.Key.ModuleId, g.Key.Module, g.Count(), g.Sum(e => e.Errors)))
            .OrderByDescending(m => m.Errors).ToList();
        return new ErrorLogDto(entries, byModule);
    }

    public async Task<List<QuestionPublicDto>> MarkedAsync(string kind, int? courseId, CancellationToken ct = default)
    {
        if (!MarkKinds.IsValid(kind)) throw AppException.Invalid("Tipo de marca inválido.");
        var ids = db.QuestionMarks.Where(m => m.ProfileId == profile.ProfileId && m.Kind == kind).Select(m => m.QuestionId);
        var scope = db.Questions.Where(q => ids.Contains(q.Id));
        if (courseId != null) scope = scope.Where(q => q.CourseId == courseId);
        var snap = await progress.LoadAsync(scope, ct);
        var list = await scope.Include(q => q.Options).Include(q => q.Module).OrderBy(q => q.CourseId).ThenBy(q => q.OriginalId).ToListAsync(ct);
        return list.Select(q => ProgressReader.ToPublic(q, snap)).ToList();
    }

    public async Task SetMarkAsync(int questionId, string kind, bool on, CancellationToken ct = default)
    {
        if (!MarkKinds.IsValid(kind)) throw AppException.Invalid("Tipo de marca inválido.");
        if (!await db.Questions.AnyAsync(q => q.Id == questionId, ct)) throw AppException.NotFound("Pregunta");
        var existing = await db.QuestionMarks.FirstOrDefaultAsync(m => m.ProfileId == profile.ProfileId && m.QuestionId == questionId && m.Kind == kind, ct);
        if (on && existing == null)
            db.QuestionMarks.Add(new QuestionMark { ProfileId = profile.ProfileId, QuestionId = questionId, Kind = kind, CreatedAt = DateTime.UtcNow });
        else if (!on && existing != null)
            db.QuestionMarks.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
