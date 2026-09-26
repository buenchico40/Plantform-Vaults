using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PlatformVault.Api.Security;
using Scalar.AspNetCore;

namespace PlatformVault.Api.Infrastructure;

/// <summary>
/// Documentación interactiva (OpenAPI generado + Scalar) SOLO en desarrollo. En QA, UAT y Producción no se publica:
/// el contrato de referencia es docs/api/platform-vault-v1.yaml.
/// </summary>
public static class ApiDocumentation
{
    public const string OpenApiPath = "/openapi";
    public const string ScalarPath = "/scalar";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) =>
        services.AddOpenApi(o => o.AddDocumentTransformer<SecuritySchemesTransformer>());

    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;
        app.MapOpenApi();
        app.MapScalarApiReference(o => o
            .WithTitle("PlatformVault API · desarrollo")
            .AddPreferredSecuritySchemes("apiClientKey", "userSession"));
        return app;
    }

    /// <summary>Rutas de documentación que no exigen API Key ni sesión (solo en desarrollo).</summary>
    public static bool IsDocumentationRequest(HttpContext context) =>
        context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment()
        && (context.Request.Path.StartsWithSegments(OpenApiPath, StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments(ScalarPath, StringComparison.OrdinalIgnoreCase));

    /// <summary>Declara las cabeceras de la Fase 1 para que Scalar pueda enviarlas (IMP-39).</summary>
    private sealed class SecuritySchemesTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Info.Title = "PlatformVault API";
            document.Info.Description = "Fase 1. Primero ejecute POST /api/v1/auth/login con la API Key y copie el sessionToken en X-User-Session.";
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["apiClientKey"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = ApiClientOptions.HeaderName,
                Description = "API Key del canal Web → API (desarrollo: %USERPROFILE%\\PlatformVaultDev\\web-api-key.txt).",
            };
            document.Components.SecuritySchemes["userSession"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = SessionMiddleware.HeaderName,
                Description = "Token opaco de sesión devuelto por POST /api/v1/auth/login.",
            };
            document.Security ??= [];
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("apiClientKey", document)] = [],
                [new OpenApiSecuritySchemeReference("userSession", document)] = [],
            });
            return Task.CompletedTask;
        }
    }
}
