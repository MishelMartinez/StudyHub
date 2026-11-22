using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StudyHub.Tests;

public class TestApp : WebApplicationFactory<Program>
{
    public static readonly string RepoRoot = FindRepoRoot();
    public static readonly string SeedPath = Path.Combine(RepoRoot, "data", "question-banks", "AWS_reStart_102_preguntas.json");
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"studyhub-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("Seed:BankPath", SeedPath);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" }) if (File.Exists(f)) File.Delete(f);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "data", "question-banks"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}

public static class HttpExtensions
{
    public static async Task<JsonElement> Json(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<JsonElement> GetJson(this HttpClient client, string url)
    {
        var r = await client.GetAsync(url);
        r.EnsureSuccessStatusCode();
        return await r.Json();
    }

    public static Task<HttpResponseMessage> PostJson(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body);

    public static async Task<HttpResponseMessage> Upload(this HttpClient client, string url, string content, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(content, System.Text.Encoding.UTF8, "application/json"), "file", fileName);
        return await client.PostAsync(url, form);
    }
}
