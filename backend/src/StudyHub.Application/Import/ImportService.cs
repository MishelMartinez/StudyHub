using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common;
using StudyHub.Domain;

namespace StudyHub.Application.Import;

public record ModuleSummaryDto(string Name, int Total, int InReview);

public class ImportReportDto
{
    public string Status { get; set; } = "vista_previa";
    public string FileName { get; set; } = "";
    public string? Title { get; set; }
    public string? DeclaredVersion { get; set; }
    public int? DeclaredTotal { get; set; }
    public string? Note { get; set; }
    public int TotalFound { get; set; }
    public int ModuleCount { get; set; }
    public int Single { get; set; }
    public int Multiple { get; set; }
    public int Reasoned { get; set; }
    public int NeedsReview { get; set; }
    public int FlaggedByValidation { get; set; }
    public List<ModuleSummaryDto> Modules { get; set; } = new();
    public List<ImportIssue> Issues { get; set; } = new();
    public bool CanCommit { get; set; }
    public bool AlreadyImported { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int MissingFromFile { get; set; }
    public int? CourseId { get; set; }
    public int? BankVersionId { get; set; }
}

public class ImportService(IAppDbContext db)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public async Task<ImportReportDto> PreviewAsync(string content, string fileName, CancellationToken ct = default)
    {
        var bank = Validate(content);
        var report = BuildReport(bank, fileName, "vista_previa");
        if (!bank.CanCommit) return report;

        var course = await FindCourseAsync(bank.File!.Titulo!, ct);
        if (course == null)
        {
            report.Added = bank.File.Preguntas!.Count;
            return report;
        }
        report.CourseId = course.Id;
        report.AlreadyImported = await db.BankVersions.AnyAsync(b => b.CourseId == course.Id && b.FileHash == bank.Hash, ct);
        var existing = await db.Questions.Where(q => q.CourseId == course.Id).Include(q => q.Options)
            .Include(q => q.Module).ToDictionaryAsync(q => q.OriginalId, ct);
        foreach (var q in bank.File.Preguntas!)
        {
            if (!existing.TryGetValue(q.Id!.Value, out var current)) report.Added++;
            else if (current.ContentHash != ToSnapshot(q, 0, bank).ContentHash()) report.Updated++;
            else report.Unchanged++;
        }
        var ids = bank.File.Preguntas!.Select(q => q.Id!.Value).ToHashSet();
        report.MissingFromFile = existing.Keys.Count(k => !ids.Contains(k));
        return report;
    }

    public async Task<ImportReportDto> CommitAsync(string content, string fileName, CancellationToken ct = default)
    {
        var bank = Validate(content);
        if (!bank.CanCommit) return BuildReport(bank, fileName, "rechazado");
        var file = bank.File!;
        var now = DateTime.UtcNow;

        var course = await FindCourseAsync(file.Titulo!, ct);
        if (course == null)
        {
            course = new Course { Title = file.Titulo!.Trim(), Slug = Text.Slug(file.Titulo!), CreatedAt = now };
            db.Courses.Add(course);
            await db.SaveChangesAsync(ct);
        }

        var previous = await db.BankVersions.Where(b => b.CourseId == course.Id && b.FileHash == bank.Hash)
            .OrderByDescending(b => b.Id).FirstOrDefaultAsync(ct);
        if (previous != null)
        {
            var same = BuildReport(bank, fileName, "sin_cambios");
            same.AlreadyImported = true;
            same.Unchanged = file.Preguntas!.Count;
            same.CourseId = course.Id;
            same.BankVersionId = previous.Id;
            return same;
        }

        var versionNumber = await db.BankVersions.CountAsync(b => b.CourseId == course.Id, ct) + 1;
        var bankVersion = new BankVersion
        {
            CourseId = course.Id, VersionNumber = versionNumber, DeclaredVersion = file.Version ?? "",
            DeclaredTotal = file.TotalPreguntas, Note = file.Nota, SourceFileName = fileName,
            FileHash = bank.Hash, RawContent = content, ImportedAt = now,
        };
        db.BankVersions.Add(bankVersion);
        await db.SaveChangesAsync(ct);

        var modules = await db.Modules.Where(m => m.CourseId == course.Id).ToDictionaryAsync(m => m.Slug, ct);
        var nextOrder = modules.Count == 0 ? 0 : modules.Values.Max(m => m.SortOrder) + 1;
        foreach (var name in file.Preguntas!.Select(q => q.Modulo!.Trim()).Distinct())
        {
            var slug = Text.Slug(name);
            if (modules.ContainsKey(slug)) continue;
            var module = new Module { CourseId = course.Id, Name = name, Slug = slug, SortOrder = nextOrder++ };
            db.Modules.Add(module);
            modules[slug] = module;
        }
        await db.SaveChangesAsync(ct);

        var existing = await db.Questions.Where(q => q.CourseId == course.Id).Include(q => q.Options)
            .ToDictionaryAsync(q => q.OriginalId, ct);
        var report = BuildReport(bank, fileName, "importado");
        foreach (var item in file.Preguntas!)
        {
            var module = modules[Text.Slug(item.Modulo!)];
            var snapshot = ToSnapshot(item, module.Id, bank);
            var hash = snapshot.ContentHash();
            if (!existing.TryGetValue(item.Id!.Value, out var question))
            {
                question = new Question { CourseId = course.Id, OriginalId = item.Id!.Value };
                db.Questions.Add(question);
                report.Added++;
            }
            else if (question.ContentHash != hash)
            {
                db.QuestionOptions.RemoveRange(question.Options);
                question.Options = new();
                report.Updated++;
            }
            else
            {
                question.LastBankVersionId = bankVersion.Id;
                report.Unchanged++;
                continue;
            }
            Apply(question, item, module.Id, snapshot, hash, bankVersion.Id, now);
        }
        var ids = file.Preguntas!.Select(q => q.Id!.Value).ToHashSet();
        report.MissingFromFile = existing.Keys.Count(k => !ids.Contains(k));
        report.CourseId = course.Id;
        report.BankVersionId = bankVersion.Id;
        bankVersion.ReportJson = Json.Serialize(report);
        await db.SaveChangesAsync(ct);
        return report;
    }

    private static ValidatedBank Validate(string content)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(content) > MaxBytes)
            throw AppException.Invalid($"El archivo supera el máximo de {MaxBytes / (1024 * 1024)} MB.");
        return BankValidator.Validate(content);
    }

    private Task<Course?> FindCourseAsync(string title, CancellationToken ct)
    {
        var slug = Text.Slug(title);
        return db.Courses.FirstOrDefaultAsync(c => c.Slug == slug, ct);
    }

    private static void Apply(Question q, BankQuestion item, int moduleId, QuestionSnapshot snapshot, string hash, int bankVersionId, DateTime now)
    {
        q.ModuleId = moduleId;
        q.Text = item.Pregunta ?? "";
        q.Type = item.Tipo ?? "";
        q.Explanation = item.Explicacion ?? "";
        q.MemoryRule = item.ReglaParaMemorizar ?? "";
        q.OriginalState = item.Estado ?? "";
        q.Source = item.Fuente;
        q.CorrectAnswersJson = Json.Serialize(item.RespuestasCorrectas ?? new());
        q.ValidationIssuesJson = Json.Serialize(snapshot.ValidationIssues);
        q.HasValidationIssues = snapshot.ValidationIssues.Count > 0;
        q.ContentHash = hash;
        q.LastBankVersionId = bankVersionId;
        q.UpdatedAt = now;
        q.CurrentVersion++;
        var order = 0;
        foreach (var o in item.Opciones ?? new())
            q.Options.Add(new QuestionOption { Key = o.Id ?? "", Text = o.Texto ?? "", IsCorrect = o.EsCorrecta == true, SortOrder = order++ });
        q.Versions.Add(new QuestionVersion
        {
            VersionNumber = q.CurrentVersion, BankVersionId = bankVersionId, ContentHash = hash,
            SnapshotJson = Json.Serialize(snapshot), CreatedAt = now,
        });
    }

    private static QuestionSnapshot ToSnapshot(BankQuestion q, int moduleId, ValidatedBank bank) => new(
        q.Id!.Value, moduleId, q.Modulo?.Trim() ?? "", q.Pregunta ?? "", q.Tipo ?? "",
        (q.Opciones ?? new()).Select(o => new OptionSnapshot(o.Id ?? "", o.Texto ?? "", o.EsCorrecta == true)).ToList(),
        q.RespuestasCorrectas ?? new(), q.Explicacion ?? "", q.ReglaParaMemorizar ?? "", q.Estado ?? "", q.Fuente,
        bank.IssuesFor(q.Id!.Value));

    private static ImportReportDto BuildReport(ValidatedBank bank, string fileName, string status)
    {
        var report = new ImportReportDto { Status = status, FileName = fileName, Issues = bank.Issues, CanCommit = bank.CanCommit };
        var file = bank.File;
        if (file == null) return report;
        report.Title = file.Titulo;
        report.DeclaredVersion = file.Version;
        report.DeclaredTotal = file.TotalPreguntas;
        report.Note = file.Nota;
        var questions = (file.Preguntas ?? new()).Where(q => q.Id != null).ToList();
        report.TotalFound = file.Preguntas?.Count ?? 0;
        report.Single = questions.Count(q => q.Tipo == QuestionTypes.Single);
        report.Multiple = questions.Count(q => q.Tipo == QuestionTypes.Multiple);
        report.Reasoned = questions.Count(q => q.Estado == OriginalStates.Reasoned);
        report.NeedsReview = questions.Count(q => q.Estado == OriginalStates.NeedsReview);
        var flaggedIds = bank.Issues.Where(i => i.QuestionId != null && i.Severity == ImportIssue.Error)
            .Select(i => i.QuestionId!.Value).ToHashSet();
        report.FlaggedByValidation = flaggedIds.Count;
        report.Modules = questions.Where(q => !string.IsNullOrWhiteSpace(q.Modulo))
            .GroupBy(q => q.Modulo!.Trim())
            .Select(g => new ModuleSummaryDto(g.Key, g.Count(),
                g.Count(q => q.Estado == OriginalStates.NeedsReview || flaggedIds.Contains(q.Id!.Value))))
            .ToList();
        report.ModuleCount = report.Modules.Count;
        return report;
    }
}
