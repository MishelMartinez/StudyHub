using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common;
using StudyHub.Domain;

namespace StudyHub.Application.Services;

public class StudyService(IAppDbContext db, ICurrentProfile profile, ProgressReader progress)
{
    public async Task<List<QuestionPublicDto>> GetModuleQuestionsAsync(int moduleId, CancellationToken ct = default)
    {
        if (!await db.Modules.AnyAsync(m => m.Id == moduleId, ct)) throw AppException.NotFound("Módulo");
        var scope = db.Questions.Where(q => q.ModuleId == moduleId);
        var snap = await progress.LoadAsync(scope, ct);
        var questions = await scope.Include(q => q.Options).Include(q => q.Module).OrderBy(q => q.OriginalId).ToListAsync(ct);
        return questions.Select(q => ProgressReader.ToPublic(q, snap)).ToList();
    }

    public async Task<QuestionPublicDto> GetQuestionAsync(int id, CancellationToken ct = default)
    {
        var scope = db.Questions.Where(q => q.Id == id);
        var q = await scope.Include(x => x.Options).Include(x => x.Module).FirstOrDefaultAsync(ct)
                ?? throw AppException.NotFound("Pregunta");
        return ProgressReader.ToPublic(q, await progress.LoadAsync(scope, ct));
    }

    public async Task<CheckResultDto> CheckAsync(CheckRequest request, CancellationToken ct = default)
    {
        var q = await db.Questions.Include(x => x.Options).Include(x => x.Module)
                    .FirstOrDefaultAsync(x => x.Id == request.QuestionId, ct)
                ?? throw AppException.NotFound("Pregunta");
        var selected = SelectionRules.Normalize(request.Selected, q.Options.Select(o => o.Key), q.Type, requireAnswer: true);
        var snapshot = QuestionSnapshot.From(q, q.Module!.Name);
        var correct = Grader.IsCorrect(q.Type, snapshot.CorrectAnswers, selected);
        db.Attempts.Add(new Attempt
        {
            ProfileId = profile.ProfileId, QuestionId = q.Id, QuestionVersion = q.CurrentVersion,
            Context = AttemptContexts.Study, SelectedJson = Json.Serialize(selected), IsCorrect = correct,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        var snap = await progress.LoadAsync(db.Questions.Where(x => x.Id == q.Id), ct);
        return new CheckResultDto(FeedbackDto.From(snapshot, selected, correct), snap.For(q.Id));
    }
}

public static class SelectionRules
{
    public static List<string> Normalize(IEnumerable<string>? selected, IEnumerable<string> validKeys, string type, bool requireAnswer)
    {
        var keys = validKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = (selected ?? Enumerable.Empty<string>()).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (list.Count != list.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw AppException.Invalid("La selección contiene opciones repetidas.");
        var unknown = list.FirstOrDefault(s => !keys.Contains(s));
        if (unknown != null) throw AppException.Invalid($"La opción «{unknown}» no existe en esta pregunta.");
        if (requireAnswer && list.Count == 0) throw AppException.Invalid("Selecciona al menos una opción.");
        if (type == QuestionTypes.Single && list.Count > 1)
            throw AppException.Invalid("Esta pregunta es de selección única.");
        var canonical = keys.ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);
        return list.Select(s => canonical[s]).ToList();
    }
}
