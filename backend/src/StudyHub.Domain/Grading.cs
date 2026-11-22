namespace StudyHub.Domain;

public static class Grader
{
    /// <summary>Single: exactly one selected and equal to the key. Multiple: set equality, order-insensitive, no extras.</summary>
    public static bool IsCorrect(string type, IEnumerable<string> correct, IEnumerable<string>? selected)
    {
        var expected = correct.Select(Normalize).ToHashSet();
        var chosenList = (selected ?? Enumerable.Empty<string>()).Select(Normalize).ToList();
        var chosen = chosenList.ToHashSet();
        if (chosen.Count == 0 || chosen.Count != chosenList.Count) return false;
        if (type == QuestionTypes.Single && (chosen.Count != 1 || expected.Count != 1)) return false;
        return expected.SetEquals(chosen);
    }

    private static string Normalize(string key) => key.Trim().ToUpperInvariant();
}

public static class Mastery
{
    public const string New = "nueva";
    public const string InProgress = "en_progreso";
    public const string WithErrors = "con_errores";
    public const string Mastered = "dominada";

    /// <summary>
    /// nueva: no attempts. con_errores: latest attempt wrong. dominada: the latest
    /// <see cref="RequiredStreak"/> attempts are all correct. en_progreso: otherwise.
    /// A single correct answer never counts as mastery.
    /// </summary>
    public const int RequiredStreak = 2;

    public static string From(IReadOnlyList<bool> attemptsOldestFirst)
    {
        if (attemptsOldestFirst.Count == 0) return New;
        if (!attemptsOldestFirst[^1]) return WithErrors;
        if (attemptsOldestFirst.Count >= RequiredStreak &&
            attemptsOldestFirst.Skip(attemptsOldestFirst.Count - RequiredStreak).All(c => c))
            return Mastered;
        return InProgress;
    }
}
