using Microsoft.AspNetCore.Mvc;
using PlatformVault.Application.Abstractions;

namespace PlatformVault.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected static PageRequest Page(int? page, int? pageSize) => new(page ?? 1, pageSize ?? 25);

    /// <summary>Versión esperada del recurso (If-Match). Obligatoria en toda modificación de objetos.</summary>
    protected string IfMatch => Request.Headers.IfMatch.ToString();

    protected void SetETag(string etag) => Response.Headers.ETag = $"\"{etag}\"";

    /// <summary>
    /// Filtros del contrato que aún no se implementan (IMP-41): se rechazan con 400 para no devolver resultados
    /// sin filtrar en silencio.
    /// </summary>
    protected void RejectUnsupported(params string[] parameters)
    {
        var present = parameters.Where(p => Request.Query.ContainsKey(p)).ToList();
        if (present.Count > 0)
            throw new ValidationFailure($"Filtro no disponible en esta versión: {string.Join(", ", present)}.");
    }
}
