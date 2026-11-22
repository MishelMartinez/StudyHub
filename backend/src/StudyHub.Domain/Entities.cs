namespace StudyHub.Domain;

public static class QuestionTypes
{
    public const string Single = "unica";
    public const string Multiple = "multiple";
    public static bool IsValid(string? type) => type is Single or Multiple;
}

public static class OriginalStates
{
    public const string Reasoned = "respuesta_razonada";
    public const string NeedsReview = "requiere_revision";
    public static bool IsKnown(string? state) => state is Reasoned or NeedsReview;
}

public static class MarkKinds
{
    public const string Favorite = "favorito";
    public const string Review = "repasar";
    public static bool IsValid(string? kind) => kind is Favorite or Review;
}

public static class ExamModes
{
    public const string Practice = "practica";
    public const string Exam = "examen";
    public static bool IsValid(string? mode) => mode is Practice or Exam;
}

public static class AttemptContexts
{
    public const string Study = "estudio";
    public const string Practice = "practica";
    public const string Exam = "examen";
}

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<Module> Modules { get; set; } = new();
    public List<BankVersion> BankVersions { get; set; } = new();
}

/// <summary>Each successfully imported file. The raw content is preserved untouched.</summary>
public class BankVersion
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public int VersionNumber { get; set; }
    public string DeclaredVersion { get; set; } = "";
    public int? DeclaredTotal { get; set; }
    public string? Note { get; set; }
    public string SourceFileName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public string RawContent { get; set; } = "";
    public string ReportJson { get; set; } = "";
    public DateTime ImportedAt { get; set; }
}

public class Module
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int SortOrder { get; set; }
    public List<Question> Questions { get; set; } = new();
}

/// <summary>Identified by (CourseId, OriginalId). Holds the current version; history lives in QuestionVersion.</summary>
public class Question
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public int ModuleId { get; set; }
    public Module? Module { get; set; }
    public int OriginalId { get; set; }
    public string Text { get; set; } = "";
    public string Type { get; set; } = QuestionTypes.Single;
    public string Explanation { get; set; } = "";
    public string MemoryRule { get; set; } = "";
    public string OriginalState { get; set; } = OriginalStates.Reasoned;
    public string? Source { get; set; }
    public string CorrectAnswersJson { get; set; } = "[]";
    public int CurrentVersion { get; set; }
    public string ContentHash { get; set; } = "";
    public string ValidationIssuesJson { get; set; } = "[]";
    public bool HasValidationIssues { get; set; }
    public int LastBankVersionId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<QuestionOption> Options { get; set; } = new();
    public List<QuestionVersion> Versions { get; set; } = new();

    public bool NeedsReview => OriginalState == OriginalStates.NeedsReview || HasValidationIssues;
}

public class QuestionOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Key { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}

public class QuestionVersion
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public int VersionNumber { get; set; }
    public int BankVersionId { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class UserProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int PassThreshold { get; set; } = 70;
    public DateTime CreatedAt { get; set; }
}

public class QuestionMark
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public int QuestionId { get; set; }
    public Question? Question { get; set; }
    public string Kind { get; set; } = MarkKinds.Favorite;
    public DateTime CreatedAt { get; set; }
}

public class Attempt
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public int QuestionId { get; set; }
    public Question? Question { get; set; }
    public int QuestionVersion { get; set; }
    public string Context { get; set; } = AttemptContexts.Study;
    public int? ExamId { get; set; }
    public string SelectedJson { get; set; } = "[]";
    public bool IsCorrect { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Exam
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public string Title { get; set; } = "";
    public string Mode { get; set; } = ExamModes.Exam;
    public string ConfigJson { get; set; } = "{}";
    public int? TimeLimitSeconds { get; set; }
    public int? SourceExamId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int PassThreshold { get; set; }
    public int Total { get; set; }
    public int Correct { get; set; }
    public int Incorrect { get; set; }
    public int Unanswered { get; set; }
    public double Percentage { get; set; }
    public bool Passed { get; set; }
    public int? DurationSeconds { get; set; }
    public List<ExamQuestion> Questions { get; set; } = new();

    public bool IsFinished => FinishedAt != null;
    public DateTime? Deadline => TimeLimitSeconds is int t ? CreatedAt.AddSeconds(t) : null;
}

/// <summary>Frozen copy of the question at exam creation, so history never changes after bank updates.</summary>
public class ExamQuestion
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public int Position { get; set; }
    public int QuestionId { get; set; }
    public int QuestionVersion { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string OptionOrderJson { get; set; } = "[]";
    public string? SelectedJson { get; set; }
    public bool Marked { get; set; }
    public bool Checked { get; set; }
    public bool? IsCorrect { get; set; }
    public DateTime? AnsweredAt { get; set; }
}
