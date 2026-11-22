using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common;
using StudyHub.Domain;

namespace StudyHub.Application.Services;

public static class ExamFilters
{
    public const string All = "todas";
    public const string New = "nuevas";
    public const string Errors = "errores";
    public const string Favorites = "favoritas";
    public const string Review = "repaso";
    public static readonly string[] Values = { All, New, Errors, Favorites, Review };
}

public class ExamConfigDto
{
    public int CourseId { get; set; }
    public List<int>? ModuleIds { get; set; }
    public double? Count { get; set; }
    public string Order { get; set; } = "aleatorio";
    public bool ShuffleOptions { get; set; }
    public string Filter { get; set; } = ExamFilters.All;
    public bool IncludeReview { get; set; }
    public double? TimeLimitMinutes { get; set; }
    public string Mode { get; set; } = ExamModes.Exam;
}

public record EligibilityDto(int Eligible, int ExcludedForReview, int Max);
public record ExamQuestionDto(
    int Position, int QuestionId, int OriginalId, string ModuleName, string Text, string Type,
    List<OptionPublicDto> Options, List<string> Selected, bool Marked, bool Checked,
    bool NeedsReview, List<string> ReviewNotes, FeedbackDto? Feedback);
public record ModuleResultDto(int ModuleId, string Module, int Total, int Correct, double Percentage);
public record ExamResultDto(
    int Total, int Correct, int Incorrect, int Unanswered, double Percentage, int PassThreshold, bool Passed,
    int? DurationSeconds, List<ModuleResultDto> ByModule);
public record ExamDto(
    int Id, int CourseId, string CourseTitle, string Title, string Mode, string Status, DateTime CreatedAt,
    DateTime? Deadline, int? TimeLimitSeconds, DateTime? FinishedAt, int Total, int Answered, ExamConfigDto Config,
    List<ExamQuestionDto> Questions, ExamResultDto? Result);
public record ExamSummaryDto(
    int Id, int CourseId, string Title, string Mode, string Status, DateTime CreatedAt, DateTime? FinishedAt,
    int Total, int Correct, double Percentage, bool Passed);
public record AnswerRequest(List<string>? Selected, bool? Marked);

public class ExamService(IAppDbContext db, ICurrentProfile profile, ProgressReader progress)
{
    public async Task<EligibilityDto> EligibilityAsync(ExamConfigDto config, CancellationToken ct = default)
    {
        var (eligible, excluded) = await EligibleQuestionsAsync(config, ct);
        return new EligibilityDto(eligible.Count, excluded, eligible.Count);
    }

    public async Task<ExamDto> CreateAsync(ExamConfigDto config, CancellationToken ct = default)
    {
        if (!ExamModes.IsValid(config.Mode)) throw AppException.Invalid("Modo inválido: usa «practica» o «examen».");
        if (config.Order is not ("aleatorio" or "secuencial")) throw AppException.Invalid("Orden inválido: usa «aleatorio» o «secuencial».");
        int? timeLimit = null;
        if (config.TimeLimitMinutes is double minutes)
        {
            if (minutes % 1 != 0 || minutes < 1 || minutes > 600)
                throw AppException.Invalid("El tiempo debe ser un número entero de minutos entre 1 y 600.");
            timeLimit = (int)minutes * 60;
        }
        var (eligible, _) = await EligibleQuestionsAsync(config, ct);
        if (config.Count is not double count || count % 1 != 0 || count < 1)
            throw AppException.Invalid("La cantidad de preguntas debe ser un número entero mayor que cero.");
        if (eligible.Count == 0) throw AppException.Invalid("No hay preguntas elegibles con esa configuración.");
        if (count > eligible.Count)
            throw AppException.Invalid($"Solo hay {eligible.Count} preguntas elegibles; elige una cantidad entre 1 y {eligible.Count}.");

        var course = await db.Courses.FirstAsync(c => c.Id == config.CourseId, ct);
        var selected = eligible.OrderBy(q => q.Module!.SortOrder).ThenBy(q => q.OriginalId).ToList();
        if (config.Order == "aleatorio") selected.Shuffle();
        selected = selected.Take((int)count).ToList();
        var title = config.Mode == ExamModes.Practice ? "Práctica" : "Examen";
        return await PersistAsync(course, $"{title} · {course.Title}", config, timeLimit, selected, null, ct);
    }

    public async Task<ExamDto> RetryErrorsAsync(int examId, CancellationToken ct = default)
    {
        var source = await LoadAsync(examId, ct);
        if (!source.IsFinished) throw AppException.Conflict("Finaliza el examen antes de repetir sus errores.");
        var ids = source.Questions.Where(q => q.IsCorrect != true).OrderBy(q => q.Position).Select(q => q.QuestionId).ToList();
        if (ids.Count == 0) throw AppException.Invalid("Este examen no tiene errores ni preguntas sin responder.");
        var questions = await db.Questions.Include(q => q.Options).Include(q => q.Module)
            .Where(q => ids.Contains(q.Id)).ToListAsync(ct);
        questions = ids.Select(id => questions.First(q => q.Id == id)).ToList();
        var config = Json.Deserialize<ExamConfigDto>(source.ConfigJson);
        config.Count = questions.Count;
        config.Filter = ExamFilters.Errors;
        var course = await db.Courses.FirstAsync(c => c.Id == source.CourseId, ct);
        return await PersistAsync(course, $"Repaso de errores · {course.Title}", config, source.TimeLimitSeconds, questions, source.Id, ct);
    }

    public async Task<List<ExamSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var exams = await db.Exams.Where(e => e.ProfileId == profile.ProfileId).OrderByDescending(e => e.Id).ToListAsync(ct);
        return exams.Select(e => new ExamSummaryDto(e.Id, e.CourseId, e.Title, e.Mode, Status(e), e.CreatedAt, e.FinishedAt,
            e.Total, e.Correct, e.Percentage, e.Passed)).ToList();
    }

    public async Task<ExamDto> GetAsync(int id, CancellationToken ct = default)
    {
        var exam = await LoadAsync(id, ct);
        if (!exam.IsFinished && exam.Deadline is DateTime d && DateTime.UtcNow > d) await FinishInternalAsync(exam, ct);
        return await ToDtoAsync(exam, ct);
    }

    public async Task<ExamDto> AnswerAsync(int id, int position, AnswerRequest request, CancellationToken ct = default)
    {
        var exam = await LoadAsync(id, ct);
        EnsureOpen(exam);
        if (exam.Deadline is DateTime d && DateTime.UtcNow > d)
        {
            await FinishInternalAsync(exam, ct);
            throw AppException.Conflict("Se agotó el tiempo; el examen se finalizó automáticamente.");
        }
        var eq = Find(exam, position);
        if (request.Selected != null)
        {
            if (eq.Checked) throw AppException.Conflict("Esta pregunta ya fue comprobada; no se puede cambiar la respuesta.");
            var snapshot = Json.Deserialize<QuestionSnapshot>(eq.SnapshotJson);
            var selected = SelectionRules.Normalize(request.Selected, snapshot.Options.Select(o => o.Key), snapshot.Type, requireAnswer: false);
            eq.SelectedJson = selected.Count == 0 ? null : Json.Serialize(selected);
            eq.AnsweredAt = selected.Count == 0 ? null : DateTime.UtcNow;
        }
        if (request.Marked is bool marked) eq.Marked = marked;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(exam, ct);
    }

    public async Task<ExamDto> CheckAsync(int id, int position, CancellationToken ct = default)
    {
        var exam = await LoadAsync(id, ct);
        EnsureOpen(exam);
        if (exam.Mode != ExamModes.Practice)
            throw AppException.Invalid("En modo examen la retroalimentación se muestra solo al finalizar.");
        var eq = Find(exam, position);
        if (eq.Checked) throw AppException.Conflict("Esta pregunta ya fue comprobada.");
        var selected = Json.Keys(eq.SelectedJson);
        if (selected.Count == 0) throw AppException.Invalid("Selecciona al menos una opción antes de comprobar.");
        Grade(exam, eq, AttemptContexts.Practice);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(exam, ct);
    }

    public async Task<ExamDto> FinishAsync(int id, CancellationToken ct = default)
    {
        var exam = await LoadAsync(id, ct);
        if (exam.IsFinished) throw AppException.Conflict("Este examen ya fue finalizado; su calificación no puede modificarse.");
        await FinishInternalAsync(exam, ct);
        return await ToDtoAsync(exam, ct);
    }

    private async Task FinishInternalAsync(Exam exam, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var eq in exam.Questions.Where(q => !q.Checked))
        {
            if (Json.Keys(eq.SelectedJson).Count == 0) { eq.IsCorrect = false; continue; }
            Grade(exam, eq, exam.Mode == ExamModes.Practice ? AttemptContexts.Practice : AttemptContexts.Exam);
        }
        exam.Total = exam.Questions.Count;
        exam.Correct = exam.Questions.Count(q => q.IsCorrect == true);
        exam.Unanswered = exam.Questions.Count(q => Json.Keys(q.SelectedJson).Count == 0);
        exam.Incorrect = exam.Total - exam.Correct - exam.Unanswered;
        exam.Percentage = exam.Total == 0 ? 0 : Math.Round(exam.Correct * 100.0 / exam.Total, 2);
        exam.Passed = exam.Percentage >= exam.PassThreshold;
        var end = exam.Deadline is DateTime d && d < now ? d : now;
        exam.DurationSeconds = (int)Math.Max(0, (end - exam.CreatedAt).TotalSeconds);
        exam.FinishedAt = now;
        await db.SaveChangesAsync(ct);
    }

    private void Grade(Exam exam, ExamQuestion eq, string context)
    {
        var snapshot = Json.Deserialize<QuestionSnapshot>(eq.SnapshotJson);
        var selected = Json.Keys(eq.SelectedJson);
        eq.IsCorrect = Grader.IsCorrect(snapshot.Type, snapshot.CorrectAnswers, selected);
        eq.Checked = true;
        db.Attempts.Add(new Attempt
        {
            ProfileId = exam.ProfileId, QuestionId = eq.QuestionId, QuestionVersion = eq.QuestionVersion,
            Context = context, ExamId = exam.Id, SelectedJson = eq.SelectedJson ?? "[]",
            IsCorrect = eq.IsCorrect.Value, CreatedAt = DateTime.UtcNow,
        });
    }

    private async Task<ExamDto> PersistAsync(Course course, string title, ExamConfigDto config, int? timeLimit,
        List<Question> questions, int? sourceExamId, CancellationToken ct)
    {
        var threshold = await db.Profiles.Where(p => p.Id == profile.ProfileId).Select(p => p.PassThreshold).FirstAsync(ct);
        var exam = new Exam
        {
            ProfileId = profile.ProfileId, CourseId = course.Id, Title = title, Mode = config.Mode,
            ConfigJson = Json.Serialize(config), TimeLimitSeconds = timeLimit, SourceExamId = sourceExamId,
            CreatedAt = DateTime.UtcNow, PassThreshold = threshold, Total = questions.Count,
        };
        var position = 1;
        foreach (var q in questions)
        {
            var snapshot = QuestionSnapshot.From(q, q.Module!.Name);
            var order = snapshot.Options.Select(o => o.Key).ToList();
            if (config.ShuffleOptions) order.Shuffle();
            exam.Questions.Add(new ExamQuestion
            {
                Position = position++, QuestionId = q.Id, QuestionVersion = q.CurrentVersion,
                SnapshotJson = Json.Serialize(snapshot), OptionOrderJson = Json.Serialize(order),
            });
        }
        db.Exams.Add(exam);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(exam, ct);
    }

    private async Task<(List<Question> Eligible, int ExcludedForReview)> EligibleQuestionsAsync(ExamConfigDto config, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == config.CourseId, ct)) throw AppException.NotFound("Curso");
        if (!ExamFilters.Values.Contains(config.Filter)) throw AppException.Invalid("Filtro inválido.");
        var scope = db.Questions.Where(q => q.CourseId == config.CourseId);
        if (config.ModuleIds is { Count: > 0 } moduleIds) scope = scope.Where(q => moduleIds.Contains(q.ModuleId));
        var snap = await progress.LoadAsync(scope, ct);
        var all = await scope.Include(q => q.Options).Include(q => q.Module).ToListAsync(ct);
        var filtered = all.Where(q => config.Filter switch
        {
            ExamFilters.New => snap.For(q.Id).Attempts == 0,
            ExamFilters.Errors => snap.Attempts.TryGetValue(q.Id, out var a) && a.Any(x => !x.IsCorrect) &&
                                  snap.For(q.Id).Mastery != Mastery.Mastered,
            ExamFilters.Favorites => snap.Favorites.Contains(q.Id),
            ExamFilters.Review => snap.Review.Contains(q.Id),
            _ => true,
        }).ToList();
        var excluded = config.IncludeReview ? 0 : filtered.Count(q => q.NeedsReview);
        if (!config.IncludeReview) filtered = filtered.Where(q => !q.NeedsReview).ToList();
        return (filtered, excluded);
    }

    private async Task<Exam> LoadAsync(int id, CancellationToken ct) =>
        await db.Exams.Include(e => e.Questions).FirstOrDefaultAsync(e => e.Id == id && e.ProfileId == profile.ProfileId, ct)
        ?? throw AppException.NotFound("Examen");

    private static void EnsureOpen(Exam exam)
    {
        if (exam.IsFinished) throw AppException.Conflict("Este examen ya fue finalizado.");
    }

    private static ExamQuestion Find(Exam exam, int position) =>
        exam.Questions.FirstOrDefault(q => q.Position == position) ?? throw AppException.NotFound("Pregunta del examen");

    private static string Status(Exam e) => e.IsFinished ? "finalizado" : "en_curso";

    private async Task<ExamDto> ToDtoAsync(Exam exam, CancellationToken ct)
    {
        var courseTitle = await db.Courses.Where(c => c.Id == exam.CourseId).Select(c => c.Title).FirstAsync(ct);
        var items = exam.Questions.OrderBy(q => q.Position).Select(eq =>
        {
            var s = Json.Deserialize<QuestionSnapshot>(eq.SnapshotJson);
            var order = Json.Keys(eq.OptionOrderJson);
            var selected = Json.Keys(eq.SelectedJson);
            var reveal = exam.IsFinished || (exam.Mode == ExamModes.Practice && eq.Checked);
            return new ExamQuestionDto(eq.Position, eq.QuestionId, s.OriginalId, s.Module, s.Text, s.Type,
                order.Select(k => new OptionPublicDto(k, s.Options.First(o => o.Key == k).Text)).ToList(),
                selected, eq.Marked, eq.Checked, s.NeedsReview, ReviewNotes.For(s.OriginalState, s.ValidationIssues),
                reveal ? FeedbackDto.From(s, selected, eq.IsCorrect == true, order) : null);
        }).ToList();

        ExamResultDto? result = null;
        if (exam.IsFinished)
        {
            var byModule = exam.Questions.Select(eq => (eq, s: Json.Deserialize<QuestionSnapshot>(eq.SnapshotJson)))
                .GroupBy(x => (x.s.ModuleId, x.s.Module))
                .Select(g =>
                {
                    var correct = g.Count(x => x.eq.IsCorrect == true);
                    return new ModuleResultDto(g.Key.ModuleId, g.Key.Module, g.Count(), correct, Math.Round(correct * 100.0 / g.Count(), 2));
                }).OrderBy(m => m.Percentage).ToList();
            result = new ExamResultDto(exam.Total, exam.Correct, exam.Incorrect, exam.Unanswered, exam.Percentage,
                exam.PassThreshold, exam.Passed, exam.DurationSeconds, byModule);
        }

        return new ExamDto(exam.Id, exam.CourseId, courseTitle, exam.Title, exam.Mode, Status(exam), exam.CreatedAt,
            exam.Deadline, exam.TimeLimitSeconds, exam.FinishedAt, exam.Questions.Count,
            exam.Questions.Count(q => Json.Keys(q.SelectedJson).Count > 0), Json.Deserialize<ExamConfigDto>(exam.ConfigJson),
            items, result);
    }
}
