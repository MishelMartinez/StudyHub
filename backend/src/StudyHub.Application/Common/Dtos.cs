namespace StudyHub.Application.Common;

public record OptionPublicDto(string Key, string Text);

public record QuestionProgressDto(int Attempts, int CorrectAttempts, bool? LastCorrect, string Mastery, DateTime? LastAttemptAt);

/// <summary>Public question: never includes correct answers, explanation, memory rule or diagrams.</summary>
public record QuestionPublicDto(
    int Id, int OriginalId, int CourseId, int ModuleId, string ModuleName, string Text, string Type,
    List<OptionPublicDto> Options, bool NeedsReview, List<string> ReviewNotes,
    bool IsFavorite, bool IsMarkedForReview, QuestionProgressDto Progress);

public record OptionFeedbackDto(string Key, string Text, bool IsCorrect, bool Selected);

public record FeedbackDto(
    bool IsCorrect, List<string> Selected, List<string> CorrectAnswers, List<OptionFeedbackDto> Options,
    string Explanation, string MemoryRule, string OriginalState, string? Source,
    bool NeedsReview, List<string> ReviewNotes)
{
    public static FeedbackDto From(QuestionSnapshot s, IReadOnlyCollection<string> selected, bool isCorrect, IReadOnlyList<string>? optionOrder = null)
    {
        var sel = selected.Select(k => k.ToUpperInvariant()).ToHashSet();
        var options = optionOrder is { Count: > 0 }
            ? optionOrder.Select(k => s.Options.First(o => o.Key == k)).ToList()
            : s.Options;
        return new FeedbackDto(isCorrect, selected.ToList(), s.CorrectAnswers,
            options.Select(o => new OptionFeedbackDto(o.Key, o.Text, o.IsCorrect, sel.Contains(o.Key.ToUpperInvariant()))).ToList(),
            s.Explanation, s.MemoryRule, s.OriginalState, s.Source, s.NeedsReview,
            Common.ReviewNotes.For(s.OriginalState, s.ValidationIssues, detailed: true));
    }
}

public record CheckRequest(int QuestionId, List<string>? Selected);
public record CheckResultDto(FeedbackDto Feedback, QuestionProgressDto Progress);
