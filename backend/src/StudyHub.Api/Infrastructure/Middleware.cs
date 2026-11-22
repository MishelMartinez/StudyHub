using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common;
using StudyHub.Infrastructure.Persistence;

namespace StudyHub.Api.Infrastructure;

public class CurrentProfile : ICurrentProfile
{
    public int ProfileId { get; set; }
}

public class ProfileMiddleware(RequestDelegate next)
{
    public const string Header = "X-Profile-Id";

    public async Task InvokeAsync(HttpContext context, AppDbContext db, CurrentProfile current)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var raw = context.Request.Headers[Header].ToString();
            if (int.TryParse(raw, out var id) && await db.Profiles.AnyAsync(p => p.Id == id))
                current.ProfileId = id;
            else if (!string.IsNullOrEmpty(raw))
                throw AppException.Invalid("El perfil indicado no existe.");
            else
                current.ProfileId = await db.Profiles.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        }
        await next(context);
    }
}

public class ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> log)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await Write(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error no controlado");
            await Write(context, 500, "Ocurrió un error inesperado. Intenta de nuevo.");
        }
    }

    private static Task Write(HttpContext context, int status, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = detail, Detail = detail },
            options: null, contentType: "application/problem+json");
    }
}
