using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common;
using StudyHub.Domain;

namespace StudyHub.Application.Services;

/// <summary>Reads per-profile progress (attempts and marks) for a set of questions.</summary>
public class ProgressReader(IAppDbContext db, ICurrentProfile profile)
{
    public record Snapshot(Dictionary<int, List<Attempt>> Attempts, HashSet<int> Favorites, HashSet<int> Review)
    {
        public QuestionProgressDto For(int questionId)
        {
            if (!Attempts.TryGetValue(questionId, out var list) || list.Count == 0)
                return new QuestionProgressDto(0, 0, null, Mastery.New, null);
            return new QuestionProgressDto(list.Count, list.Count(a => a.IsCorrect), list[^1].IsCorrect,
                Mastery.From(list.Select(a => a.IsCorrect).ToList()), list[^1].CreatedAt);
        }
    }

    public async Task<Snapshot> LoadAsync(IQueryable<Question> scope, CancellationToken ct = default)
    {
        var ids = scope.Select(q => q.Id);
        var attempts = await db.Attempts.Where(a => a.ProfileId == profile.ProfileId && ids.Contains(a.QuestionId))
            .OrderBy(a => a.Id).ToListAsync(ct);
        var marks = await db.QuestionMarks.Where(m => m.ProfileId == profile.ProfileId && ids.Contains(m.QuestionId))
            .Select(m => new { m.QuestionId, m.Kind }).ToListAsync(ct);
        return new Snapshot(
            attempts.GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.ToList()),
            marks.Where(m => m.Kind == MarkKinds.Favorite).Select(m => m.QuestionId).ToHashSet(),
            marks.Where(m => m.Kind == MarkKinds.Review).Select(m => m.QuestionId).ToHashSet());
    }

    public static QuestionPublicDto ToPublic(Question q, Snapshot progress) => new(
        q.Id, q.OriginalId, q.CourseId, q.ModuleId, q.Module?.Name ?? "", q.Text, q.Type,
        q.Options.OrderBy(o => o.SortOrder).Select(o => new OptionPublicDto(o.Key, o.Text)).ToList(),
        q.NeedsReview, ReviewNotes.For(q.OriginalState, Json.Deserialize<List<string>>(q.ValidationIssuesJson)),
        progress.Favorites.Contains(q.Id), progress.Review.Contains(q.Id), progress.For(q.Id));
}
