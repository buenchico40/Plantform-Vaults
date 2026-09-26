using System.Reflection;
using System.Text.RegularExpressions;

namespace PlatformVault.Api.Tests;

/// <summary>Reglas de arquitectura obligatorias del prompt §2 (IMP-34).</summary>
public sealed partial class ArchitectureTests
{
    private static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PlatformVault.sln")))
                dir = dir.Parent;
            return dir!.FullName;
        }
    }

    private static IEnumerable<string> SourceFiles(string project) =>
        Directory.EnumerateFiles(Path.Combine(Root, "src", project), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [Fact]
    public void Domain_and_application_have_no_infrastructure_dependencies()
    {
        foreach (var assembly in new[] { typeof(Domain.Objects.ManagedObject).Assembly, typeof(Application.Abstractions.IClock).Assembly })
        {
            var references = assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
            Assert.DoesNotContain(references, r => r.StartsWith("Dapper", StringComparison.Ordinal));
            Assert.DoesNotContain(references, r => r.Contains("SqlClient", StringComparison.Ordinal));
            Assert.DoesNotContain(references, r => r.StartsWith("PlatformVault.Infrastructure", StringComparison.Ordinal));
            Assert.DoesNotContain(references, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Web_project_does_not_reference_data_or_security_layers()
    {
        var csproj = File.ReadAllText(Path.Combine(Root, "src", "PlatformVault.Web", "PlatformVault.Web.csproj"));
        foreach (var forbidden in new[] { "PlatformVault.Infrastructure", "PlatformVault.Application", "PlatformVault.Domain", "Dapper", "SqlClient", "EntityFramework" })
            Assert.DoesNotContain($"Include=\"{forbidden}", csproj.Replace("../", "", StringComparison.Ordinal).Replace("..\\", "", StringComparison.Ordinal), StringComparison.Ordinal);
        foreach (var file in SourceFiles("PlatformVault.Web"))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("Microsoft.Data.SqlClient", text, StringComparison.Ordinal);
            Assert.DoesNotContain("using Dapper", text, StringComparison.Ordinal);
            Assert.DoesNotContain("PlatformVault.Infrastructure", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void No_entity_framework_anywhere()
    {
        foreach (var csproj in Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories))
            Assert.DoesNotContain("EntityFrameworkCore", File.ReadAllText(csproj), StringComparison.Ordinal);
    }

    [Fact]
    public void Csharp_contains_no_embedded_or_dynamic_sql_IMP03()
    {
        var sql = EmbeddedSql();
        foreach (var project in new[] { "PlatformVault.Infrastructure", "PlatformVault.Api", "PlatformVault.Application", "PlatformVault.Web" })
        {
            foreach (var file in SourceFiles(project))
            {
                var match = sql.Match(File.ReadAllText(file));
                Assert.False(match.Success, $"SQL embebido en {Path.GetFileName(file)}: {match.Value}");
            }
        }
    }

    [Fact]
    public void Every_database_call_targets_a_stored_procedure()
    {
        var calls = ProcedureCall();
        foreach (var file in SourceFiles("PlatformVault.Infrastructure"))
        {
            foreach (Match m in calls.Matches(File.ReadAllText(file)))
                Assert.Matches(@"^\[?(identity|app|vault|audit|report)\]?\.usp_\w+$", m.Groups[1].Value);
        }
    }

    [Fact]
    public void Browser_code_never_stores_secrets_or_tokens()
    {
        var js = File.ReadAllText(Path.Combine(Root, "src", "PlatformVault.Web", "wwwroot", "js", "site.js"));
        Assert.DoesNotContain("localStorage.", js, StringComparison.Ordinal);
        Assert.DoesNotContain("sessionStorage.", js, StringComparison.Ordinal);
        Assert.DoesNotContain("X-API-Key", js, StringComparison.Ordinal);
        Assert.DoesNotContain("X-User-Session", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Forbidden_domain_terms_are_not_used()
    {
        var forbidden = ForbiddenTerms();
        foreach (var project in new[] { "PlatformVault.Domain", "PlatformVault.Application", "PlatformVault.Infrastructure", "PlatformVault.Api", "PlatformVault.Web" })
        {
            foreach (var file in SourceFiles(project))
                Assert.False(forbidden.IsMatch(File.ReadAllText(file)), $"Término no permitido en {Path.GetFileName(file)}");
        }
    }

    [GeneratedRegex(@"""\s*(SELECT\s+.+\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|EXEC(UTE)?\s*\(|sp_executesql)", RegexOptions.IgnoreCase)]
    private static partial Regex EmbeddedSql();

    [GeneratedRegex(@"(?:ExecuteAsync|QueryAsync<[^>]+>|QuerySingleOrDefaultAsync<[^>]+>|QueryMultipleAsync|ContactsAsync|new SqlCommand)\(""([^""]+)""")]
    private static partial Regex ProcedureCall();

    [GeneratedRegex(@"\b(HelpDesk|Help Desk|Ticket|Incident|SLA)\b")]
    private static partial Regex ForbiddenTerms();
}
