using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using StudyHub.Domain;

namespace StudyHub.Application.Common;

public interface IAppDbContext
{
    DbSet<Course> Courses { get; }
    DbSet<BankVersion> BankVersions { get; }
    DbSet<Module> Modules { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionOption> QuestionOptions { get; }
    DbSet<QuestionVersion> QuestionVersions { get; }
    DbSet<UserProfile> Profiles { get; }
    DbSet<QuestionMark> QuestionMarks { get; }
    DbSet<Attempt> Attempts { get; }
    DbSet<Exam> Exams { get; }
    DbSet<ExamQuestion> ExamQuestions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Profile resolved per request (header X-Profile-Id). Explicit persistent profile for the local MVP.</summary>
public interface ICurrentProfile
{
    int ProfileId { get; }
}

public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public static AppException NotFound(string what) => new(404, $"{what} no encontrado.");
    public static AppException Invalid(string message) => new(400, message);
    public static AppException Conflict(string message) => new(409, message);
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
    public static List<string> Keys(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() : Deserialize<List<string>>(json);
}

public static class Text
{
    public static string Slug(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        }
        var slug = string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length == 0 ? "sin-nombre" : slug;
    }

    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public static class ListExtensions
{
    public static void Shuffle<T>(this IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
