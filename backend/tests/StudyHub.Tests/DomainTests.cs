using StudyHub.Application.Import;
using StudyHub.Domain;

namespace StudyHub.Tests;

public class GraderTests
{
    [Fact] public void Single_correct() => Assert.True(Grader.IsCorrect("unica", new[] { "C" }, new[] { "C" }));
    [Fact] public void Single_wrong() => Assert.False(Grader.IsCorrect("unica", new[] { "C" }, new[] { "A" }));
    [Fact] public void Single_with_two_selected_is_wrong() => Assert.False(Grader.IsCorrect("unica", new[] { "C" }, new[] { "C", "A" }));
    [Fact] public void Multiple_any_order() => Assert.True(Grader.IsCorrect("multiple", new[] { "C", "D" }, new[] { "D", "C" }));
    [Fact] public void Multiple_partial_is_wrong() => Assert.False(Grader.IsCorrect("multiple", new[] { "C", "D" }, new[] { "C" }));
    [Fact] public void Multiple_extra_is_wrong() => Assert.False(Grader.IsCorrect("multiple", new[] { "C", "D" }, new[] { "C", "D", "E" }));
    [Fact] public void Empty_is_wrong() => Assert.False(Grader.IsCorrect("multiple", new[] { "C", "D" }, Array.Empty<string>()));
}

public class MasteryTests
{
    [Fact] public void New_without_attempts() => Assert.Equal(Mastery.New, Mastery.From(new List<bool>()));
    [Fact] public void Single_correct_is_not_mastery() => Assert.Equal(Mastery.InProgress, Mastery.From(new List<bool> { true }));
    [Fact] public void Last_wrong_has_errors() => Assert.Equal(Mastery.WithErrors, Mastery.From(new List<bool> { true, true, false }));
    [Fact] public void Two_consecutive_correct_is_mastered() => Assert.Equal(Mastery.Mastered, Mastery.From(new List<bool> { false, true, true }));
}

public class ValidatorTests
{
    private static string Bank(string questions, int total = 1) =>
        $$"""{"titulo":"T","version":"1","totalPreguntas":{{total}},"nota":"n","preguntas":[{{questions}}]}""";

    private const string Ok = """{"id":1,"modulo":"M","pregunta":"¿P?","tipo":"unica","opciones":[{"id":"A","texto":"a","esCorrecta":true},{"id":"B","texto":"b","esCorrecta":false}],"respuestasCorrectas":["A"],"explicacion":"e","reglaParaMemorizar":"r","estado":"respuesta_razonada","fuente":"f"}""";

    [Fact]
    public void Real_bank_matches_pilot_criteria()
    {
        var result = BankValidator.Validate(File.ReadAllText(TestApp.SeedPath));
        Assert.True(result.CanCommit);
        var qs = result.File!.Preguntas!;
        Assert.Equal(102, qs.Count);
        Assert.Equal(25, qs.Select(q => q.Modulo).Distinct().Count());
        Assert.Equal(78, qs.Count(q => q.Tipo == "unica"));
        Assert.Equal(24, qs.Count(q => q.Tipo == "multiple"));
        Assert.Equal(90, qs.Count(q => q.Estado == "respuesta_razonada"));
        Assert.Equal(12, qs.Count(q => q.Estado == "requiere_revision"));
        Assert.Contains(result.Issues, i => i.QuestionId == 33 && i.Code == "cardinalidad_multiple");
        Assert.Contains(result.Issues, i => i.QuestionId == 33 && i.Code == "seleccion_incoherente");
    }

    [Fact]
    public void Malformed_json_is_rejected()
    {
        var r = BankValidator.Validate("{\"titulo\": ");
        Assert.False(r.CanCommit);
        Assert.Contains(r.Issues, i => i.Code == "json_malformado");
    }

    [Fact]
    public void Duplicate_ids_block_import()
    {
        var r = BankValidator.Validate(Bank(Ok + "," + Ok, 2));
        Assert.False(r.CanCommit);
        Assert.Contains(r.Issues, i => i.Code == "id_duplicado");
    }

    [Fact]
    public void Incoherent_answers_are_flagged_not_fixed()
    {
        var bad = Ok.Replace("\"respuestasCorrectas\":[\"A\"]", "\"respuestasCorrectas\":[\"B\"]");
        var r = BankValidator.Validate(Bank(bad));
        Assert.True(r.CanCommit);
        Assert.Contains(r.Issues, i => i.Code == "escorrecta_incoherente");
        Assert.Equal(new[] { "B" }, r.File!.Preguntas![0].RespuestasCorrectas);
    }

    [Fact]
    public void Missing_reference_and_declared_count_mismatch()
    {
        var bad = Ok.Replace("\"respuestasCorrectas\":[\"A\"]", "\"respuestasCorrectas\":[\"Z\"]");
        var r = BankValidator.Validate(Bank(bad, total: 5));
        Assert.Contains(r.Issues, i => i.Code == "respuesta_inexistente");
        Assert.Contains(r.Issues, i => i.Code == "conteo_distinto");
    }
    [Fact]
    public void Null_entries_are_reported_not_crashing()
    {
        var r = BankValidator.Validate(Bank("null"));
        Assert.False(r.CanCommit);
        Assert.Contains(r.Issues, i => i.Code == "pregunta_nula");
        var opt = BankValidator.Validate(Bank(Ok.Replace("\"opciones\":[", "\"opciones\":[null,")));
        Assert.False(opt.CanCommit);
        Assert.Contains(opt.Issues, i => i.Code == "opcion_nula");
    }

    [Fact]
    public void Option_ids_differing_only_in_case_are_duplicates()
    {
        var r = BankValidator.Validate(Bank(Ok.Replace("{\"id\":\"B\"", "{\"id\":\"a\"")));
        Assert.False(r.CanCommit);
        Assert.Contains(r.Issues, i => i.Code == "opcion_duplicada");
    }

    [Fact]
    public void Structural_question_errors_block_commit()
    {
        var r = BankValidator.Validate(Bank(Ok.Replace("\"modulo\":\"M\",", "")));
        Assert.False(r.CanCommit);
        Assert.Contains(r.Issues, i => i.Code == "modulo_faltante");
    }
}
