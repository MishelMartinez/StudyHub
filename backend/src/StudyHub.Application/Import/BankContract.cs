using System.Text.Json.Serialization;

namespace StudyHub.Application.Import;

/// <summary>Import contract (root): titulo, version, totalPreguntas, nota, preguntas[].</summary>
public class BankFile
{
    [JsonPropertyName("titulo")] public string? Titulo { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("totalPreguntas")] public int? TotalPreguntas { get; set; }
    [JsonPropertyName("nota")] public string? Nota { get; set; }
    [JsonPropertyName("preguntas")] public List<BankQuestion>? Preguntas { get; set; }
}

public class BankQuestion
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("modulo")] public string? Modulo { get; set; }
    [JsonPropertyName("pregunta")] public string? Pregunta { get; set; }
    [JsonPropertyName("tipo")] public string? Tipo { get; set; }
    [JsonPropertyName("opciones")] public List<BankOption>? Opciones { get; set; }
    [JsonPropertyName("respuestasCorrectas")] public List<string>? RespuestasCorrectas { get; set; }
    [JsonPropertyName("explicacion")] public string? Explicacion { get; set; }
    [JsonPropertyName("reglaParaMemorizar")] public string? ReglaParaMemorizar { get; set; }
    [JsonPropertyName("estado")] public string? Estado { get; set; }
    [JsonPropertyName("fuente")] public string? Fuente { get; set; }
}

public class BankOption
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("texto")] public string? Texto { get; set; }
    [JsonPropertyName("esCorrecta")] public bool? EsCorrecta { get; set; }
}
