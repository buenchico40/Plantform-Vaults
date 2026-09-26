using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PlatformVault.Api.Security;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Domain.Common;

namespace PlatformVault.Api.Infrastructure;

/// <summary>Traduce los errores a ProblemDetails (RFC 7807) sin detalle interno ni valores sensibles.</summary>
public sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var identity = httpContext.RequestServices.GetRequiredService<RequestIdentity>();
        var (status, code, title, errors) = exception switch
        {
            NotFoundFailure f => (404, f.Code, f.Message, null),
            ForbiddenFailure f => (403, f.Code, f.Message, null),
            ConcurrencyFailure f => (409, f.Code, f.Message, null),
            ConflictFailure f => (409, f.Code, f.Message, null),
            ValidationFailure f => (400, f.Code, f.Message, f.Errors),
            ReauthenticationRequiredFailure f => (401, f.Code, f.Message, null),
            AuthenticationFailure f => (401, f.Code, f.Message, null),
            AuditUnavailableFailure f => (424, f.Code, f.Message, null),
            DomainException d => (422, d.Code, d.Message, null),
            BadHttpRequestException => (400, "BAD_REQUEST", "La petición no es válida.", null),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (499, "CANCELLED", "Petición cancelada.", null),
            _ => (500, "INTERNAL_ERROR", "Se produjo un error interno. Indique el identificador de correlación al soporte.",
                (IReadOnlyDictionary<string, string[]>?)null),
        };

        if (status >= 500 || exception is AuditUnavailableFailure)
            logger.LogError(exception is AuditUnavailableFailure a ? a.Inner : exception, "Error no controlado {Code}", code);
        else
            logger.LogInformation("Petición rechazada {Status} {Code}", status, code);

        var problem = new ProblemDetails { Status = status, Title = title, Type = "about:blank", Instance = httpContext.Request.Path };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = identity.CorrelationId;
        if (errors is { Count: > 0 })
            problem.Extensions["errors"] = errors;

        httpContext.Response.StatusCode = status == 499 ? 400 : status;
        await httpContext.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json", cancellationToken);
        return true;
    }
}

/// <summary>
/// Protección contra mass assignment (IMP-09): los DTO son explícitos y el JSON rechaza miembros desconocidos.
/// Este filtro audita el intento antes de que [ApiController] responda 400.
/// </summary>
public sealed class MassAssignmentAuditFilter(AuditLogger audit) : IAsyncActionFilter, IOrderedFilter
{
    public int Order => -3000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            var unmapped = context.ModelState.Values.SelectMany(v => v.Errors)
                .Any(e => e.Exception is JsonException || e.ErrorMessage.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase));
            if (unmapped)
            {
                await audit.DeniedAsync(AuditActions.MassAssignmentRejected, "Api", context.HttpContext.Request.Path,
                    new { method = context.HttpContext.Request.Method }, context.HttpContext.RequestAborted);
            }
        }
        await next();
    }
}
