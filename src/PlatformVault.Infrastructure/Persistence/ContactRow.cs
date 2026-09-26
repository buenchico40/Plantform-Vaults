using PlatformVault.Application.Access;

namespace PlatformVault.Infrastructure.Persistence;

/// <summary>Fila de contacto: se mapea por nombre de columna, sin depender del orden del procedimiento.</summary>
internal sealed class ContactRow
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
}

internal static class ContactQueries
{
    public static async Task<IReadOnlyList<UserContact>> ContactsAsync(this StoredProcedures sp, string procedure, object parameters, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<ContactRow>(procedure, parameters, ct);
        return rows.Select(r => new UserContact(r.UserId, r.DisplayName, r.Email)).ToList();
    }
}
