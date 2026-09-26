using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlatformVault.Api.Infrastructure;
using PlatformVault.Api.Security;
using PlatformVault.Application.Abstractions;
using PlatformVault.Infrastructure;
using PlatformVault.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext());
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 256 * 1024);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationLayer();
builder.Services.Configure<ApiClientOptions>(builder.Configuration.GetSection(ApiClientOptions.Section));
builder.Services.Configure<JobOptions>(builder.Configuration.GetSection(JobOptions.Section));
if (builder.Configuration.GetValue(JobOptions.Section + ":Enabled", true) && !args.Contains(BootstrapAdministrator.Argument))
    builder.Services.AddHostedService<JobScheduler>();

builder.Services.AddScoped<MassAssignmentAuditFilter>();
builder.Services
    .AddControllers(o =>
    {
        o.Filters.AddService<MassAssignmentAuditFilter>();
        // Enumerados de consulta con los valores del contrato (p. ej. criticality=Crítico).
        o.ModelBinderProviders.Insert(0, new ContractEnumModelBinderProvider());
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        // IMP-09: un miembro no declarado en el DTO rechaza la petición (mass assignment).
        o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    });
builder.Services.AddProblemDetails();
// El generador de OpenAPI usa estas opciones: los enumerados se documentan como texto con los valores del contrato.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.Services.AddApiDocumentation();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
// Límite de intentos por IP del usuario final (no por la IP del servidor Web, que comparten todos los usuarios).
var loginPermitsPerMinute = builder.Configuration.GetValue("Security:LoginAttemptsPerMinutePerClient", 20);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.RequestServices.GetRequiredService<RequestIdentity>().ClientIp ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermitsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<KeyHealthCheck>("kek");

var app = builder.Build();

if (args.Contains(BootstrapAdministrator.Argument))
{
    Environment.ExitCode = await BootstrapAdministrator.RunAsync(app.Services, args);
    return;
}

app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseMiddleware<ApiClientMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<SessionMiddleware>();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapControllers();
app.MapApiDocumentation();

app.Run();

/// <summary>Salud de la base de datos: ejecuta un procedimiento de solo lectura con la cuenta técnica.</summary>
internal sealed class DatabaseHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IAreaRepository>().GetAllAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Base de datos no disponible.", ex);
        }
    }
}

/// <summary>Salud de la KEK: el certificado se carga y cifra/descifra un valor de prueba (DEC-35).</summary>
internal sealed class KeyHealthCheck(IServiceProvider services) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var crypto = services.GetRequiredService<IEnvelopeEncryption>();
            var probe = crypto.Encrypt("probe"u8, Guid.Empty, 0, 0, PlatformVault.Domain.Objects.PayloadKind.Text);
            var plain = crypto.Decrypt(probe);
            return Task.FromResult(plain.AsSpan().SequenceEqual("probe"u8) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("KEK inconsistente."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("KEK no disponible.", ex));
        }
    }
}

public partial class Program
{
}
