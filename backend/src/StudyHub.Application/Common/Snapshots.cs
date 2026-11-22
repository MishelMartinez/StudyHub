using StudyHub.Domain;

namespace StudyHub.Application.Common;

public record OptionSnapshot(string Key, string Text, bool IsCorrect);

/// <summary>Frozen question content stored in QuestionVersion and ExamQuestion.</summary>
public record QuestionSnapshot(
    int OriginalId,
    int ModuleId,
    string Module,
    string Text,
    string Type,
    List<OptionSnapshot> Options,
    List<string> CorrectAnswers,
    string Explanation,
    string MemoryRule,
    string OriginalState,
    string? Source,
    List<string> ValidationIssues)
{
    public bool NeedsReview => OriginalState == OriginalStates.NeedsReview || ValidationIssues.Count > 0;

    public static QuestionSnapshot From(Question q, string moduleName) => new(
        q.OriginalId, q.ModuleId, moduleName, q.Text, q.Type,
        q.Options.OrderBy(o => o.SortOrder).Select(o => new OptionSnapshot(o.Key, o.Text, o.IsCorrect)).ToList(),
        Json.Keys(q.CorrectAnswersJson), q.Explanation, q.MemoryRule, q.OriginalState, q.Source,
        Json.Deserialize<List<string>>(q.ValidationIssuesJson));

    public string ContentHash() =>
        Common.Text.Sha256(Json.Serialize(this with { ModuleId = 0, ValidationIssues = new() }));
}

public static class ReviewNotes
{
    public const string OriginalReview =
        "El banco original marca esta pregunta como «requiere revisión»: la respuesta puede ser ambigua o no coincidir con ninguna opción exacta.";

    public static List<string> For(string originalState, IEnumerable<string> validationIssues)
    {
        var notes = new List<string>();
        if (originalState == OriginalStates.NeedsReview) notes.Add(OriginalReview);
        notes.AddRange(validationIssues.Select(i => $"Validación de importación: {i}"));
        return notes;
    }
}
