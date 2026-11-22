using System.Text.Json;
using System.Text.RegularExpressions;
using StudyHub.Application.Common;
using StudyHub.Domain;

namespace StudyHub.Application.Import;

public record ImportIssue(int? QuestionId, string Severity, string Code, string Message)
{
    public const string Error = "error";
    public const string Warning = "advertencia";
}

public class ValidatedBank
{
    public BankFile? File { get; init; }
    public List<ImportIssue> Issues { get; } = new();
    public string Hash { get; init; } = "";

    /// <summary>Codes that make a question impossible to store; answer-key inconsistencies only flag it for review.</summary>
    public static readonly HashSet<string> BlockingCodes = new()
    {
        "id_duplicado", "enunciado_faltante", "modulo_faltante", "tipo_invalido",
        "opciones_insuficientes", "opcion_incompleta", "opcion_duplicada", "opcion_nula",
    };

    /// <summary>Root-level errors and structural question errors block the import.</summary>
    public bool CanCommit => File?.Preguntas != null && !Issues.Any(i => i.Severity == ImportIssue.Error &&
                             (i.QuestionId == null || BlockingCodes.Contains(i.Code)));

    public List<string> IssuesFor(int questionId) => Issues
        .Where(i => i.QuestionId == questionId && i.Severity == ImportIssue.Error)
        .Select(i => i.Message).ToList();
}

/// <summary>Validates without modifying data: inconsistencies are reported and flagged for review, never fixed silently.</summary>
public static partial class BankValidator
{
    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dos"] = 2, ["tres"] = 3, ["cuatro"] = 4, ["cinco"] = 5, ["2"] = 2, ["3"] = 3, ["4"] = 4, ["5"] = 5,
    };

    [GeneratedRegex(@"\b(?:seleccione|elija|escoja|selecciona|elige)\s+(dos|tres|cuatro|cinco|[2-5])\b", RegexOptions.IgnoreCase)]
    private static partial Regex SelectCountRegex();

    public static ValidatedBank Validate(string content)
    {
        var hash = Text.Sha256(content);
        BankFile? file;
        try
        {
            using (JsonDocument.Parse(content)) { }
            file = JsonSerializer.Deserialize<BankFile>(content);
        }
        catch (JsonException ex)
        {
            var bad = new ValidatedBank { Hash = hash };
            var where = ex.Path is { Length: > 0 } ? $" (ruta {ex.Path}, línea {ex.LineNumber + 1})" : "";
            bad.Issues.Add(new(null, ImportIssue.Error, "json_malformado", $"JSON malformado o con tipos inválidos{where}."));
            return bad;
        }

        var result = new ValidatedBank { File = file, Hash = hash };
        if (file == null)
        {
            result.Issues.Add(new(null, ImportIssue.Error, "json_vacio", "El archivo no contiene un objeto JSON."));
            return result;
        }
        if (string.IsNullOrWhiteSpace(file.Titulo))
            result.Issues.Add(new(null, ImportIssue.Error, "titulo_faltante", "Falta el campo raíz «titulo»."));
        if (file.Preguntas == null)
        {
            result.Issues.Add(new(null, ImportIssue.Error, "preguntas_faltantes", "Falta el arreglo raíz «preguntas»."));
            return result;
        }
        if (file.Preguntas.Count == 0)
            result.Issues.Add(new(null, ImportIssue.Error, "preguntas_vacias", "El arreglo «preguntas» está vacío."));
        if (file.TotalPreguntas == null)
            result.Issues.Add(new(null, ImportIssue.Warning, "total_faltante", "Falta «totalPreguntas»; no se puede verificar el conteo declarado."));
        else if (file.TotalPreguntas != file.Preguntas.Count)
            result.Issues.Add(new(null, ImportIssue.Warning, "conteo_distinto",
                $"«totalPreguntas» declara {file.TotalPreguntas} pero el archivo contiene {file.Preguntas.Count}."));

        var nullCount = file.Preguntas.RemoveAll(q => q == null);
        if (nullCount > 0)
            result.Issues.Add(new(null, ImportIssue.Error, "pregunta_nula", $"El arreglo «preguntas» contiene {nullCount} elemento(s) nulo(s)."));

        for (var i = 0; i < file.Preguntas.Count; i++)
        {
            var q = file.Preguntas[i];
            if (q.Id == null)
            {
                result.Issues.Add(new(null, ImportIssue.Error, "id_faltante", $"La pregunta en la posición {i + 1} no tiene «id»."));
                continue;
            }
            ValidateQuestion(q, result.Issues);
        }

        foreach (var dup in file.Preguntas.Where(q => q.Id != null).GroupBy(q => q.Id!.Value).Where(g => g.Count() > 1))
            result.Issues.Add(new(dup.Key, ImportIssue.Error, "id_duplicado", $"El id {dup.Key} aparece {dup.Count()} veces en el banco."));

        return result;
    }

    private static void ValidateQuestion(BankQuestion q, List<ImportIssue> issues)
    {
        var id = q.Id!.Value;
        void Err(string code, string msg) => issues.Add(new(id, ImportIssue.Error, code, msg));
        void Warn(string code, string msg) => issues.Add(new(id, ImportIssue.Warning, code, msg));

        if (string.IsNullOrWhiteSpace(q.Pregunta)) Err("enunciado_faltante", "Falta el enunciado («pregunta»).");
        if (string.IsNullOrWhiteSpace(q.Modulo)) Err("modulo_faltante", "Falta «modulo».");
        if (!QuestionTypes.IsValid(q.Tipo)) Err("tipo_invalido", $"«tipo» debe ser «unica» o «multiple» (recibido: «{q.Tipo}»).");
        if (!OriginalStates.IsKnown(q.Estado)) Warn("estado_desconocido", $"«estado» desconocido: «{q.Estado}».");

        if (q.Opciones != null && q.Opciones.RemoveAll(o => o == null) > 0)
            Err("opcion_nula", "«opciones» contiene elementos nulos.");
        var options = q.Opciones ?? new();
        if (options.Count < 2) Err("opciones_insuficientes", "La pregunta necesita al menos dos opciones.");
        if (options.Any(o => string.IsNullOrWhiteSpace(o.Id) || string.IsNullOrWhiteSpace(o.Texto)))
            Err("opcion_incompleta", "Hay opciones sin «id» o sin «texto».");
        if (options.Any(o => o.EsCorrecta == null))
            Err("escorrecta_faltante", "Hay opciones sin «esCorrecta».");
        var optionIds = options.Where(o => !string.IsNullOrWhiteSpace(o.Id)).Select(o => o.Id!).ToList();
        foreach (var dup in optionIds.GroupBy(x => x.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            Err("opcion_duplicada", $"La opción «{dup.Key}» está repetida (sin distinguir mayúsculas).");

        var answers = q.RespuestasCorrectas ?? new();
        if (answers.Count == 0) Err("respuestas_faltantes", "«respuestasCorrectas» está vacío.");
        foreach (var missing in answers.Where(a => !optionIds.Contains(a)))
            Err("respuesta_inexistente", $"«respuestasCorrectas» referencia la opción «{missing}», que no existe.");

        var flagged = options.Where(o => o.EsCorrecta == true && o.Id != null).Select(o => o.Id!).ToHashSet();
        if (!flagged.SetEquals(answers))
            Err("escorrecta_incoherente",
                $"«esCorrecta» marca [{string.Join(", ", flagged.Order())}] pero «respuestasCorrectas» indica [{string.Join(", ", answers)}].");

        var correctCount = answers.Distinct().Count();
        if (q.Tipo == QuestionTypes.Single && correctCount != 1)
            Err("cardinalidad_unica", $"Pregunta de selección única con {correctCount} respuestas correctas.");
        if (q.Tipo == QuestionTypes.Multiple && correctCount < 2)
            Err("cardinalidad_multiple", $"Pregunta de selección múltiple con {correctCount} respuesta(s) correcta(s).");

        var match = q.Pregunta == null ? null : SelectCountRegex().Match(q.Pregunta);
        if (match is { Success: true } && NumberWords.TryGetValue(match.Groups[1].Value, out var expected))
        {
            if (expected != correctCount)
                Err("seleccion_incoherente", $"El enunciado pide «{match.Value}» pero hay {correctCount} respuesta(s) correcta(s).");
            if (q.Tipo != QuestionTypes.Multiple)
                Err("tipo_incoherente", $"El enunciado pide «{match.Value}» pero el tipo es «{q.Tipo}».");
        }
    }
}
