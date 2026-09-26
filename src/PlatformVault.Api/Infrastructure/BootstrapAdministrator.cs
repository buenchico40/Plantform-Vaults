using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Users;
using PlatformVault.Domain.Identity;

namespace PlatformVault.Api.Infrastructure;

/// <summary>
/// Alta del primer Administrador: <c>PlatformVault.Api --bootstrap-admin &lt;usuario&gt; "&lt;Nombre visible&gt;"</c>.
/// Solo funciona si no existe ningún Administrador activo. Imprime una contraseña temporal que obliga a cambiarla.
/// </summary>
public static class BootstrapAdministrator
{
    public const string Argument = "--bootstrap-admin";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        var index = Array.IndexOf(args, Argument);
        if (index < 0 || args.Length < index + 3)
        {
            Console.Error.WriteLine($"Uso: {Argument} <usuario> \"<Nombre visible>\"");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var users = provider.GetRequiredService<IUserDirectory>();
        if ((await users.GetActiveByRoleAsync(SystemRoles.Administrator, CancellationToken.None)).Count > 0)
        {
            Console.Error.WriteLine("Ya existe un Administrador activo: use la administración de usuarios.");
            return 1;
        }

        var identity = provider.GetRequiredService<IIdentityService>();
        var password = TemporaryPasswordGenerator.Generate();
        var (userId, errors) = await identity.CreateUserAsync(new NewUser(args[index + 1], args[index + 2], null, null, null), password, Guid.Empty,
            CancellationToken.None);
        if (errors.Count == 0)
            errors = await identity.SetRolesAsync(userId, [SystemRoles.Administrator], Guid.Empty, CancellationToken.None);
        if (errors.Count > 0)
        {
            Console.Error.WriteLine(string.Join(Environment.NewLine, errors));
            return 1;
        }

        await provider.GetRequiredService<AuditLogger>().RecordForAsync(ActorTypes.System, null, "bootstrap", AuditActions.UserCreated, "User",
            userId.ToString(), AuditResults.Success, new { userName = args[index + 1], roles = new[] { SystemRoles.Administrator }, bootstrap = true },
            CancellationToken.None);
        Console.WriteLine($"Administrador creado: {args[index + 1]}");
        Console.WriteLine($"Contraseña temporal (se muestra una sola vez; debe cambiarse en el primer inicio): {password}");
        return 0;
    }
}
