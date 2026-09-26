using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformVault.Application.Abstractions;
using PlatformVault.Infrastructure.Cryptography;
using PlatformVault.Infrastructure.Identity;
using PlatformVault.Infrastructure.Notifications;
using PlatformVault.Infrastructure.Persistence;

namespace PlatformVault.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.Section))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "Database:ConnectionString es obligatorio.").ValidateOnStart();
        services.AddOptions<KeyProtectionOptions>().Bind(configuration.GetSection(KeyProtectionOptions.Section))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ActiveThumbprint), "KeyProtection:ActiveThumbprint es obligatorio.").ValidateOnStart();
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.Section));

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<DbSession>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<DbSession>());
        services.AddScoped<StoredProcedures>();
        services.AddScoped<IObjectRepository, ObjectRepository>();
        services.AddScoped<ISecretPayloadStore, SecretPayloadStore>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IAccessRequestRepository, AccessRequestRepository>();
        services.AddScoped<ITemporaryAccessRepository, TemporaryAccessRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IExpirationRepository, ExpirationRepository>();
        services.AddScoped<AuditRepository>();
        services.AddScoped<IAuditTrail>(sp => sp.GetRequiredService<AuditRepository>());
        services.AddScoped<IAuditReader>(sp => sp.GetRequiredService<AuditRepository>());
        services.AddScoped<IAreaRepository, AreaRepository>();
        services.AddScoped<IDashboardReader, DashboardReader>();
        services.AddScoped<IJobRunRepository, JobRunRepository>();
        services.AddScoped<NotificationRepository>();
        services.AddScoped<INotificationQueue>(sp => sp.GetRequiredService<NotificationRepository>());
        services.AddScoped<INotificationOutbox>(sp => sp.GetRequiredService<NotificationRepository>());
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddSingleton<IJobLock, SqlJobLock>();
        services.AddSingleton<INotificationSender, SmtpNotificationSender>();

        services.AddSingleton<IEnvelopeEncryption, EnvelopeEncryption>();
        services.AddSingleton<ICertificateInspector, CertificateInspector>();

        // IMP-25: política de contraseñas y bloqueo con las opciones de ASP.NET Core Identity.
        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 15;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = true;
                o.Password.RequiredUniqueChars = 5;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                o.Lockout.AllowedForNewUsers = true;
                o.User.RequireUniqueEmail = false;
            })
            .AddUserStore<UserStore>()
            .AddPasswordValidator<PasswordHistoryValidator>()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ISessionService, SessionService>();
        return services;
    }
}
