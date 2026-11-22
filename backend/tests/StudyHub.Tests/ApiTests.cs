using System.Net.Http.Json;
using System.Net;
using System.Text.Json;

namespace StudyHub.Tests;

public class ApiTests : IClassFixture<TestApp>
{
    private readonly HttpClient _client;
    public ApiTests(TestApp app) => _client = app.CreateClient();

    private async Task<int> CourseId() => (await _client.GetJson("/api/courses"))[0].GetProperty("id").GetInt32();

    private async Task<int> QuestionId(int originalId)
    {
        var page = await _client.GetJson($"/api/admin/questions?search={originalId}&pageSize=200");
        return page.GetProperty("items").EnumerateArray().First(q => q.GetProperty("originalId").GetInt32() == originalId).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Seed_import_creates_modules_dynamically()
    {
        var course = (await _client.GetJson("/api/courses"))[0];
        Assert.Equal(102, course.GetProperty("total").GetInt32());
        Assert.Equal(25, course.GetProperty("moduleCount").GetInt32());
        Assert.Equal(12, course.GetProperty("inReview").GetInt32());
        var modules = await _client.GetJson($"/api/courses/{course.GetProperty("id").GetInt32()}/modules");
        Assert.Equal(25, modules.GetArrayLength());
    }

    [Fact]
    public async Task Reimport_same_file_is_idempotent()
    {
        var content = await File.ReadAllTextAsync(TestApp.SeedPath);
        var r = await _client.Upload("/api/import/commit", content, "AWS_reStart_102_preguntas.json");
        var report = await r.Json();
        Assert.Equal("sin_cambios", report.GetProperty("status").GetString());
        Assert.Equal(102, (await _client.GetJson("/api/courses"))[0].GetProperty("total").GetInt32());
        var courseId = await CourseId();
        Assert.Single((await _client.GetJson("/api/admin/banks")).EnumerateArray(), b => b.GetProperty("courseId").GetInt32() == courseId);
    }

    [Fact]
    public async Task Malformed_upload_returns_422_report()
    {
        var r = await _client.Upload("/api/import/commit", "{ nope", "roto.json");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        Assert.Equal("rechazado", (await r.Json()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task New_module_via_json_and_version_update_keep_history()
    {
        var bank = """{"titulo":"Curso de prueba","version":"1","totalPreguntas":1,"nota":"","preguntas":[{"id":1,"modulo":"Módulo nuevo","pregunta":"¿Uno?","tipo":"unica","opciones":[{"id":"A","texto":"Sí","esCorrecta":true},{"id":"B","texto":"No","esCorrecta":false}],"respuestasCorrectas":["A"],"explicacion":"x","reglaParaMemorizar":"r","estado":"respuesta_razonada","fuente":"f"}]}""";
        var first = await (await _client.Upload("/api/import/commit", bank, "prueba.json")).Json();
        Assert.Equal(1, first.GetProperty("added").GetInt32());
        var updated = bank.Replace("¿Uno?", "¿Uno, actualizado?").Replace("\"version\":\"1\"", "\"version\":\"2\"");
        var preview = await (await _client.Upload("/api/import/preview", updated, "prueba.json")).Json();
        Assert.Equal(1, preview.GetProperty("updated").GetInt32());
        var second = await (await _client.Upload("/api/import/commit", updated, "prueba.json")).Json();
        Assert.Equal("importado", second.GetProperty("status").GetString());
        Assert.Equal(1, second.GetProperty("updated").GetInt32());

        var courseId = first.GetProperty("courseId").GetInt32();
        var modules = await _client.GetJson($"/api/courses/{courseId}/modules");
        Assert.Equal("Módulo nuevo", modules[0].GetProperty("name").GetString());
        var q = (await _client.GetJson($"/api/admin/questions?courseId={courseId}")).GetProperty("items")[0];
        var detail = await _client.GetJson($"/api/admin/questions/{q.GetProperty("id").GetInt32()}");
        Assert.Equal(2, detail.GetProperty("versions").GetArrayLength());
    }

    [Fact]
    public async Task Study_question_does_not_leak_answers()
    {
        var id = await QuestionId(1);
        var raw = await (await _client.GetAsync($"/api/study/questions/{id}")).Content.ReadAsStringAsync();
        foreach (var leak in new[] { "esCorrecta", "isCorrect", "correctAnswers", "explanation", "memoryRule", "11 nueves de durabilidad" })
            Assert.DoesNotContain(leak, raw, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(1, new[] { "C" }, true)]
    [InlineData(1, new[] { "A" }, false)]
    [InlineData(4, new[] { "D", "C" }, true)]
    [InlineData(4, new[] { "C" }, false)]
    [InlineData(4, new[] { "C", "D", "E" }, false)]
    public async Task Study_check_grades_deterministically(int originalId, string[] selected, bool expected)
    {
        var id = await QuestionId(originalId);
        var r = await (await _client.PostJson("/api/study/check", new { questionId = id, selected })).Json();
        Assert.Equal(expected, r.GetProperty("feedback").GetProperty("isCorrect").GetBoolean());
        Assert.False(string.IsNullOrEmpty(r.GetProperty("feedback").GetProperty("explanation").GetString()));
    }

    [Fact]
    public async Task Question_30_is_visible_with_warning_and_excluded_from_exams_by_default()
    {
        var id = await QuestionId(30);
        var q = await _client.GetJson($"/api/study/questions/{id}");
        Assert.True(q.GetProperty("needsReview").GetBoolean());
        Assert.NotEqual(0, q.GetProperty("reviewNotes").GetArrayLength());

        var courseId = await CourseId();
        var e = await (await _client.PostJson("/api/exams/eligible", new { courseId, filter = "todas" })).Json();
        Assert.Equal(102 - 12, e.GetProperty("eligible").GetInt32());
        Assert.Equal(12, e.GetProperty("excludedForReview").GetInt32());
        var withReview = await (await _client.PostJson("/api/exams/eligible", new { courseId, filter = "todas", includeReview = true })).Json();
        Assert.Equal(102, withReview.GetProperty("eligible").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2.5)]
    [InlineData(91)]
    public async Task Invalid_counts_are_rejected(double count)
    {
        var r = await _client.PostJson("/api/exams", new { courseId = await CourseId(), count, mode = "examen" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Exam_flow_hides_keys_persists_order_and_blocks_double_finish()
    {
        var courseId = await CourseId();
        var create = await _client.PostJson("/api/exams", new { courseId, count = 10, mode = "examen", order = "aleatorio", shuffleOptions = true });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var rawCreate = await create.Content.ReadAsStringAsync();
        Assert.DoesNotContain("correctAnswers", rawCreate);
        var exam = JsonDocument.Parse(rawCreate).RootElement;
        var id = exam.GetProperty("id").GetInt32();
        var questions = exam.GetProperty("questions").EnumerateArray().ToList();
        Assert.Equal(10, questions.Count);
        Assert.Equal(10, questions.Select(q => q.GetProperty("questionId").GetInt32()).Distinct().Count());
        Assert.DoesNotContain(questions, q => q.GetProperty("needsReview").GetBoolean());

        var reloaded = await _client.GetJson($"/api/exams/{id}");
        Assert.Equal(
            questions.Select(q => q.GetProperty("questionId").GetInt32()),
            reloaded.GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("questionId").GetInt32()));
        Assert.Equal(
            questions.Select(q => string.Join(",", q.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("key").GetString()))),
            reloaded.GetProperty("questions").EnumerateArray().Select(q => string.Join(",", q.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("key").GetString()))));

        var first = questions[0];
        var key = first.GetProperty("options")[0].GetProperty("key").GetString();
        var answer = await _client.PutAsJsonAsync($"/api/exams/{id}/questions/1", new { selected = new[] { key } });
        var answerRaw = await answer.Content.ReadAsStringAsync();
        Assert.DoesNotContain("correctAnswers", answerRaw);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync($"/api/exams/{id}/questions/1/check", null)).StatusCode);

        var finish = await (await _client.PostAsync($"/api/exams/{id}/finish", null)).Json();
        var result = finish.GetProperty("result");
        Assert.Equal(10, result.GetProperty("total").GetInt32());
        Assert.Equal(9, result.GetProperty("unanswered").GetInt32());
        Assert.Equal(10, result.GetProperty("correct").GetInt32() + result.GetProperty("incorrect").GetInt32() + result.GetProperty("unanswered").GetInt32());
        Assert.True(finish.GetProperty("questions")[0].GetProperty("feedback").GetProperty("correctAnswers").GetArrayLength() > 0);

        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/exams/{id}/finish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync($"/api/exams/{id}/questions/2", new { selected = new[] { "A" } })).StatusCode);

        var retry = await (await _client.PostAsync($"/api/exams/{id}/retry-errors", null)).Json();
        Assert.Equal(10 - result.GetProperty("correct").GetInt32(), retry.GetProperty("total").GetInt32());

        var errors = await _client.GetJson($"/api/errors?courseId={courseId}");
        Assert.True(errors.GetProperty("entries").GetArrayLength() >= 0);
    }

    [Fact]
    public async Task Practice_mode_reveals_feedback_after_check_only()
    {
        var courseId = await CourseId();
        var exam = await (await _client.PostJson("/api/exams", new { courseId, count = 3, mode = "practica", order = "secuencial" })).Json();
        var id = exam.GetProperty("id").GetInt32();
        Assert.Equal(JsonValueKind.Null, exam.GetProperty("questions")[0].GetProperty("feedback").ValueKind);
        await _client.PutAsJsonAsync($"/api/exams/{id}/questions/1", new { selected = new[] { "C" } });
        var checkedExam = await (await _client.PostAsync($"/api/exams/{id}/questions/1/check", null)).Json();
        var feedback = checkedExam.GetProperty("questions")[0].GetProperty("feedback");
        Assert.True(feedback.GetProperty("isCorrect").GetBoolean());
        Assert.Equal(JsonValueKind.Null, checkedExam.GetProperty("questions")[1].GetProperty("feedback").ValueKind);
    }

    [Fact]
    public async Task Profiles_are_isolated()
    {
        var p = await (await _client.PostJson("/api/profiles", new { name = "Otro perfil" })).Json();
        var otherId = p.GetProperty("id").GetInt32();
        var qid = await QuestionId(2);
        await _client.PutAsync($"/api/marks/favorito/{qid}", null);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/marks/favorito");
        req.Headers.Add("X-Profile-Id", otherId.ToString());
        var other = await (await _client.SendAsync(req)).Json();
        Assert.Equal(0, other.GetArrayLength());
        var mine = await _client.GetJson("/api/marks/favorito");
        Assert.Contains(mine.EnumerateArray(), q => q.GetProperty("id").GetInt32() == qid);
    }
}
