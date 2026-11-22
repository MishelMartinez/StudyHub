using System.Text;
using Microsoft.AspNetCore.Mvc;
using StudyHub.Application.Common;
using StudyHub.Application.Import;
using StudyHub.Application.Services;

namespace StudyHub.Api.Controllers;

[ApiController]
[Route("api/import")]
public class ImportController(ImportService import) : ControllerBase
{
    [HttpPost("preview")]
    [RequestSizeLimit(ImportService.MaxBytes + 64 * 1024)]
    public async Task<ImportReportDto> Preview(IFormFile file, CancellationToken ct) =>
        await import.PreviewAsync(await ReadAsync(file, ct), file.FileName, ct);

    [HttpPost("commit")]
    [RequestSizeLimit(ImportService.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<ImportReportDto>> Commit(IFormFile file, CancellationToken ct)
    {
        var report = await import.CommitAsync(await ReadAsync(file, ct), file.FileName, ct);
        return report.Status == "rechazado" ? UnprocessableEntity(report) : Ok(report);
    }

    private static async Task<string> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0) throw AppException.Invalid("Selecciona un archivo JSON.");
        if (file.Length > ImportService.MaxBytes) throw AppException.Invalid("El archivo supera el máximo de 5 MB.");
        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new AppException(415, "Por ahora solo se aceptan bancos JSON. La importación desde PDF llegará en una fase posterior.");
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }
}

[ApiController]
[Route("api")]
public class CatalogController(CatalogService catalog) : ControllerBase
{
    [HttpGet("dashboard")] public Task<DashboardDto> Dashboard(CancellationToken ct) => catalog.DashboardAsync(ct);
    [HttpGet("courses")] public Task<List<CourseStatsDto>> Courses(CancellationToken ct) => catalog.CoursesAsync(ct);
    [HttpGet("courses/{courseId:int}/modules")] public Task<List<ModuleStatsDto>> Modules(int courseId, CancellationToken ct) => catalog.ModulesAsync(courseId, ct);

    [HttpGet("rules")]
    public Task<List<RuleGroupDto>> Rules([FromQuery] int? courseId, [FromQuery] int? moduleId, [FromQuery] string? search, CancellationToken ct) =>
        catalog.RulesAsync(courseId, moduleId, search, ct);

    [HttpGet("errors")] public Task<ErrorLogDto> Errors([FromQuery] int? courseId, CancellationToken ct) => catalog.ErrorLogAsync(courseId, ct);

    [HttpGet("marks/{kind}")]
    public Task<List<QuestionPublicDto>> Marked(string kind, [FromQuery] int? courseId, CancellationToken ct) => catalog.MarkedAsync(kind, courseId, ct);

    [HttpPut("marks/{kind}/{questionId:int}")]
    public async Task<NoContentResult> Mark(string kind, int questionId, CancellationToken ct)
    {
        await catalog.SetMarkAsync(questionId, kind, true, ct);
        return NoContent();
    }

    [HttpDelete("marks/{kind}/{questionId:int}")]
    public async Task<NoContentResult> Unmark(string kind, int questionId, CancellationToken ct)
    {
        await catalog.SetMarkAsync(questionId, kind, false, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/study")]
public class StudyController(StudyService study) : ControllerBase
{
    [HttpGet("modules/{moduleId:int}/questions")]
    public Task<List<QuestionPublicDto>> ModuleQuestions(int moduleId, CancellationToken ct) => study.GetModuleQuestionsAsync(moduleId, ct);

    [HttpGet("questions/{id:int}")] public Task<QuestionPublicDto> Question(int id, CancellationToken ct) => study.GetQuestionAsync(id, ct);
    [HttpPost("check")] public Task<CheckResultDto> Check(CheckRequest request, CancellationToken ct) => study.CheckAsync(request, ct);
}

[ApiController]
[Route("api/exams")]
public class ExamsController(ExamService exams) : ControllerBase
{
    [HttpGet] public Task<List<ExamSummaryDto>> List(CancellationToken ct) => exams.ListAsync(ct);
    [HttpPost("eligible")] public Task<EligibilityDto> Eligible(ExamConfigDto config, CancellationToken ct) => exams.EligibilityAsync(config, ct);
    [HttpPost] public Task<ExamDto> Create(ExamConfigDto config, CancellationToken ct) => exams.CreateAsync(config, ct);
    [HttpGet("{id:int}")] public Task<ExamDto> Get(int id, CancellationToken ct) => exams.GetAsync(id, ct);

    [HttpPut("{id:int}/questions/{position:int}")]
    public Task<ExamDto> Answer(int id, int position, AnswerRequest request, CancellationToken ct) => exams.AnswerAsync(id, position, request, ct);

    [HttpPost("{id:int}/questions/{position:int}/check")]
    public Task<ExamDto> Check(int id, int position, CancellationToken ct) => exams.CheckAsync(id, position, ct);

    [HttpPost("{id:int}/finish")] public Task<ExamDto> Finish(int id, CancellationToken ct) => exams.FinishAsync(id, ct);
    [HttpPost("{id:int}/retry-errors")] public Task<ExamDto> RetryErrors(int id, CancellationToken ct) => exams.RetryErrorsAsync(id, ct);
}

[ApiController]
[Route("api/admin")]
public class AdminController(AdminService admin) : ControllerBase
{
    [HttpGet("questions")]
    public Task<PagedDto<AdminQuestionDto>> Questions([FromQuery] int? courseId, [FromQuery] int? moduleId, [FromQuery] string? type,
        [FromQuery] string? state, [FromQuery] string? search, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        admin.QuestionsAsync(courseId, moduleId, type, state, search, page, pageSize, ct);

    [HttpGet("questions/{id:int}")] public Task<AdminQuestionDetailDto> Question(int id, CancellationToken ct) => admin.QuestionAsync(id, ct);
    [HttpGet("banks")] public Task<List<BankVersionDto>> Banks(CancellationToken ct) => admin.BanksAsync(ct);

    [HttpGet("banks/{id:int}/file")]
    public async Task<FileContentResult> BankFile(int id, CancellationToken ct)
    {
        var (name, content) = await admin.BankFileAsync(id, ct);
        return File(Encoding.UTF8.GetBytes(content), "application/json", name);
    }
}

[ApiController]
[Route("api/profiles")]
public class ProfilesController(ProfileService profiles) : ControllerBase
{
    [HttpGet] public Task<List<ProfileDto>> List(CancellationToken ct) => profiles.ListAsync(ct);
    [HttpPost] public Task<ProfileDto> Create(ProfileRequest request, CancellationToken ct) => profiles.CreateAsync(request, ct);
    [HttpPut("{id:int}")] public Task<ProfileDto> Update(int id, ProfileRequest request, CancellationToken ct) => profiles.UpdateAsync(id, request, ct);
}
