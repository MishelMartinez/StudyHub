using System.Text.Encodings.Web;
using System.Text.Unicode;
using StudyHub.Api.Infrastructure;
using StudyHub.Application;
using StudyHub.Application.Common;
using StudyHub.Infrastructure;
using StudyHub.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CurrentProfile>();
builder.Services.AddScoped<ICurrentProfile>(sp => sp.GetRequiredService<CurrentProfile>());
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
    {
        var first = ctx.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => m.Length > 0);
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 400, Title = "Datos inválidos en la solicitud.", Detail = first ?? "Revisa los valores enviados.",
        };
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem);
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "StudyHub API", Version = "v1" }));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

app.UseMiddleware<ErrorMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<ProfileMiddleware>();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
