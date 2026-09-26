using System.Diagnostics;
using Microsoft.Extensions.Options;
using PlatformVault.Infrastructure.Persistence;

namespace PlatformVault.Infrastructure.Tests;

/// <summary>
/// Base de datos de integración en LocalDB (IMP-34; sin contenedores). Se despliega una vez por ejecución con los mismos
/// scripts de /database. Variables: PV_TEST_SERVER (por defecto (localdb)\MSSQLLocalDB) y PV_TEST_SKIP_DEPLOY=1.
/// </summary>
public sealed class DatabaseFixture
{
    public const string DatabaseName = "PlatformVaultIntegrationTests";

    public DatabaseFixture()
    {
        var server = Environment.GetEnvironmentVariable("PV_TEST_SERVER") ?? @"(localdb)\MSSQLLocalDB";
        ConnectionString = $"Server={server};Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name=PlatformVault.Tests";
        if (Environment.GetEnvironmentVariable("PV_TEST_SKIP_DEPLOY") == "1")
            return;

        var root = FindRepositoryRoot();
        var deploy = Path.Combine(root, "database", "deploy.ps1");
        var info = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{deploy}\" -Server \"{server}\" -DatabaseName {DatabaseName}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(600_000);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"No se pudo desplegar la base de pruebas.\n{output.Result}\n{error.Result}");
    }

    public string ConnectionString { get; }

    public (DbSession Session, StoredProcedures Sp) Open()
    {
        var session = new DbSession(Options.Create(new DatabaseOptions { ConnectionString = ConnectionString }));
        return (session, new StoredProcedures(session));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PlatformVault.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró PlatformVault.sln.");
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
