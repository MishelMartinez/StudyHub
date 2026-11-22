using Microsoft.Extensions.DependencyInjection;
using StudyHub.Application.Import;
using StudyHub.Application.Services;

namespace StudyHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddScoped<ImportService>()
        .AddScoped<ProgressReader>()
        .AddScoped<StudyService>()
        .AddScoped<ExamService>()
        .AddScoped<CatalogService>()
        .AddScoped<AdminService>()
        .AddScoped<ProfileService>();
}
