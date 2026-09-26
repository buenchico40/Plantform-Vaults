using PlatformVault.Api.Security;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Authorization;
using PlatformVault.Application.Jobs;
using PlatformVault.Application.Notifications;
using PlatformVault.Application.Objects;

namespace PlatformVault.Api.Infrastructure;

public static class ApplicationRegistration
{
    /// <summary>Registra los handlers CQRS y los trabajos por reflexión sobre el ensamblado Application (IMP-37).</summary>
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        var handlerInterfaces = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>) };
        foreach (var type in typeof(ICommandHandler<,>).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
                services.AddScoped(contract, type);
            if (typeof(IBackgroundJob).IsAssignableFrom(type))
            {
                services.AddScoped(type);
                services.AddScoped(typeof(IBackgroundJob), type);
            }
        }

        services.AddScoped<RequestIdentity>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<RequestIdentity>());
        services.AddScoped<IRequestContext>(sp => sp.GetRequiredService<RequestIdentity>());
        services.AddScoped<AuditLogger>();
        services.AddScoped<ObjectAuthorizer>();
        services.AddScoped<Notifier>();
        services.AddScoped<OwnerValidator>();
        services.AddScoped<ObjectCommandContext>();
        services.AddScoped<PayloadAccessGuard>();
        return services;
    }
}
