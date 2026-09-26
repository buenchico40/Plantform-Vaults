using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using PlatformVault.Web.Api;

namespace PlatformVault.Web.Controllers;

public sealed class ObjectFilter
{
    public string? Text { get; set; }
    public string? Type { get; set; }
    public string? Criticality { get; set; }
    public string? LifecycleState { get; set; }
    public string? ExpirationStatus { get; set; }
    public bool WithoutOwner { get; set; }
    public string SortBy { get; set; } = "Name";
    public bool SortDescending { get; set; }
    public int Page { get; set; } = 1;
}

public sealed class ObjectListModel
{
    public ObjectFilter Filter { get; set; } = new();
    public Paged<ObjectSummary> Result { get; set; } = new();
}

public sealed class ObjectForm
{
    [Required] public string Type { get; set; } = "Secret";
    [Required, MaxLength(50)] public string Subtype { get; set; } = "";
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    [Required] public string Criticality { get; set; } = "Medio";
    [Required] public string Sensitivity { get; set; } = "Confidencial";
    [Required] public string Environment { get; set; } = "Producción";
    public Guid AreaId { get; set; }
    public Guid? FunctionalOwnerId { get; set; }
    public Guid? TechnicalOwnerId { get; set; }
    public string CustodyMode { get; set; } = "Internal";
    public DateTime? ExpirationDate { get; set; }
    public bool NoExpirationJustified { get; set; }

    /// <summary>Atributos del tipo, uno por línea: clave=valor.</summary>
    [MaxLength(4000)] public string? Attributes { get; set; }

    [MaxLength(65536), DataType(DataType.Password)] public string? InitialValue { get; set; }
    public IFormFile? File { get; set; }
    [MaxLength(256), DataType(DataType.Password)] public string? ContainerPassword { get; set; }
}

public sealed class EditForm
{
    [Required] public string ETag { get; set; } = "";
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool NoExpirationJustified { get; set; }
    [MaxLength(4000)] public string? Attributes { get; set; }
    [Required(ErrorMessage = "El motivo es obligatorio."), MinLength(5), MaxLength(500)] public string Reason { get; set; } = "";
}

public sealed class ActionForm
{
    [Required] public string ETag { get; set; } = "";
    [Required(ErrorMessage = "El motivo es obligatorio."), MinLength(5), MaxLength(500)] public string Reason { get; set; } = "";
    public string? Action { get; set; }
    public string? Criticality { get; set; }
    public string? Sensitivity { get; set; }
    public Guid? FunctionalOwnerId { get; set; }
    public Guid? TechnicalOwnerId { get; set; }
    public List<Guid> GroupIds { get; set; } = [];
    [MaxLength(65536), DataType(DataType.Password)] public string? NewValue { get; set; }
    public IFormFile? File { get; set; }
    [MaxLength(256), DataType(DataType.Password)] public string? ContainerPassword { get; set; }
    public DateTime? NewExpirationDate { get; set; }
}

public sealed class DownloadForm
{
    [Required(ErrorMessage = "Confirme su contraseña."), MaxLength(256), DataType(DataType.Password)] public string Password { get; set; } = "";
    [MaxLength(256), DataType(DataType.Password)] public string? FilePassword { get; set; }
    public string Part { get; set; } = "PrivateKeyPfx";
}

public sealed class RevealBody
{
    [Required, MaxLength(256)] public string Password { get; set; } = "";
}

public sealed class ObjectDetailModel
{
    public ObjectDetail Object { get; set; } = new();
    public List<VersionEntry> Versions { get; set; } = [];
    public List<AuditEventView>? Audit { get; set; }
    public List<TemporaryAccessView> MyAccesses { get; set; } = [];
    public List<GroupSummary> AvailableGroups { get; set; } = [];
    public List<SubtypeView> Subtypes { get; set; } = [];
}

public sealed class ObjectFormModel
{
    public ObjectForm Form { get; set; } = new();
    public List<AreaView> Areas { get; set; } = [];
    public List<SubtypeView> Subtypes { get; set; } = [];
}

public sealed class ObjectsController(PlatformApi api) : Controller
{
    public async Task<IActionResult> Index([FromQuery] ObjectFilter filter, CancellationToken ct)
    {
        var query = new List<string> { $"page={Math.Max(1, filter.Page)}", "pageSize=25", $"sortBy={Uri.EscapeDataString(filter.SortBy)}",
            $"sortDescending={filter.SortDescending.ToString().ToLowerInvariant()}" };
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) query.Add($"{name}={Uri.EscapeDataString(value)}");
        }
        Add("q", filter.Text);
        Add("type", filter.Type);
        Add("criticality", filter.Criticality);
        Add("lifecycleState", filter.LifecycleState);
        Add("expirationStatus", filter.ExpirationStatus);
        if (filter.WithoutOwner) query.Add("orphanOwner=true");
        var result = await api.GetAsync<Paged<ObjectSummary>>("objects?" + string.Join('&', query), ct);
        return View(new ObjectListModel { Filter = filter, Result = result });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var model = new ObjectDetailModel
        {
            Object = await api.GetAsync<ObjectDetail>($"objects/{id}", ct),
            Versions = await api.GetAsync<List<VersionEntry>>($"objects/{id}/versions", ct),
            MyAccesses = (await api.GetAsync<List<TemporaryAccessView>>("temporary-access?onlyCurrent=true", ct)).Where(a => a.ObjectId == id).ToList(),
            AvailableGroups = (await api.GetAsync<Paged<GroupSummary>>("groups?status=Activo&pageSize=200", ct)).Items,
            Subtypes = await api.GetAsync<List<SubtypeView>>("catalog/subtypes", ct),
        };
        try
        {
            model.Audit = await api.GetAsync<List<AuditEventView>>($"objects/{id}/audit-timeline", ct);
        }
        catch (ApiException ex) when (ex.Status == System.Net.HttpStatusCode.Forbidden)
        {
            model.Audit = null; // Sin permiso de auditoría: la pestaña no se muestra.
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct) => View(await FormModelAsync(new ObjectForm(), ct));

    [HttpPost]
    [RequestSizeLimit(200_000)]
    public async Task<IActionResult> Create(ObjectForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(await FormModelAsync(form, ct));
        var file = await ReadFileAsync(form.File, ct);
        try
        {
            var created = await api.PostAsync<ObjectCreated>("objects", new
            {
                type = form.Type,
                subtype = form.Subtype,
                name = form.Name,
                description = form.Description,
                criticality = form.Criticality,
                sensitivity = form.Sensitivity,
                environment = form.Environment,
                areaId = form.AreaId,
                functionalOwnerId = form.FunctionalOwnerId,
                technicalOwnerId = form.TechnicalOwnerId,
                custodyMode = form.CustodyMode,
                expirationDate = ToUtc(form.ExpirationDate),
                noExpirationJustified = form.NoExpirationJustified,
                attributes = ParseAttributes(form.Attributes),
                initialValue = form.Type is "CryptographicKey" or "Certificate" ? null : NullIfEmpty(form.InitialValue),
                keyMaterialBase64 = form.Type == "CryptographicKey" ? file : null,
                certificateFileBase64 = form.Type == "Certificate" ? file : null,
                certificateContainerPassword = NullIfEmpty(form.ContainerPassword),
            }, ct);
            TempData["Info"] = $"Objeto {created.Code} registrado en estado Borrador.";
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409 or 422)
        {
            ModelState.AddModelError(string.Empty, Infrastructure.ApiExceptionFilter.Describe(ex));
            form.InitialValue = null;
            form.ContainerPassword = null;
            return View(await FormModelAsync(form, ct));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, EditForm form, CancellationToken ct)
    {
        if (!Valid()) return Back(id);
        await api.PatchAsync<WriteResult>($"objects/{id}", new
        {
            name = form.Name,
            description = form.Description,
            expirationDate = ToUtc(form.ExpirationDate),
            noExpirationJustified = form.NoExpirationJustified,
            attributes = ParseAttributes(form.Attributes),
            reason = form.Reason,
        }, form.ETag, ct);
        return Done(id, "Metadatos actualizados.");
    }

    [HttpPost]
    public async Task<IActionResult> Classification(Guid id, ActionForm form, CancellationToken ct)
    {
        if (!Valid()) return Back(id);
        await api.PutAsync<WriteResult>($"objects/{id}/classification",
            new { criticality = form.Criticality, sensitivity = form.Sensitivity, reason = form.Reason }, form.ETag, ct);
        return Done(id, "Clasificación actualizada.");
    }

    [HttpPost]
    public async Task<IActionResult> State(Guid id, ActionForm form, CancellationToken ct)
    {
        if (!Valid()) return Back(id);
        var result = await api.PostAsync<WriteResult>($"objects/{id}/state", new { action = form.Action, reason = form.Reason }, ct, form.ETag);
        return Done(id, result.RevokedAccesses > 0 ? $"Estado actualizado. Accesos temporales revocados: {result.RevokedAccesses}." : "Estado actualizado.");
    }

    [HttpPost]
    public async Task<IActionResult> Owners(Guid id, ActionForm form, CancellationToken ct)
    {
        if (!Valid()) return Back(id);
        await api.PutAsync<WriteResult>($"objects/{id}/owners",
            new { functionalOwnerId = form.FunctionalOwnerId, technicalOwnerId = form.TechnicalOwnerId, reason = form.Reason }, form.ETag, ct);
        return Done(id, "Propietarios actualizados.");
    }

    [HttpPost]
    public async Task<IActionResult> Groups(Guid id, ActionForm form, CancellationToken ct)
    {
        if (!Valid()) return Back(id);
        await api.PutAsync<WriteResult>($"objects/{id}/groups", new { groupIds = form.GroupIds, reason = form.Reason }, form.ETag, ct);
        return Done(id, "Grupos actualizados.");
    }

    [HttpPost]
    [RequestSizeLimit(200_000)]
    public async Task<IActionResult> Value(Guid id, ActionForm form, [FromForm] string type, CancellationToken ct)
    {
        if (!Valid()) return Back(id);
        var file = await ReadFileAsync(form.File, ct);
        await api.PutAsync<WriteResult>($"objects/{id}/value", new
        {
            newValue = type is "CryptographicKey" or "Certificate" ? null : NullIfEmpty(form.NewValue),
            keyMaterialBase64 = type == "CryptographicKey" ? file : null,
            certificateFileBase64 = type == "Certificate" ? file : null,
            certificateContainerPassword = NullIfEmpty(form.ContainerPassword),
            newExpirationDate = ToUtc(form.NewExpirationDate),
            reason = form.Reason,
        }, form.ETag, ct);
        return Done(id, "Valor actualizado. Se generó una nueva versión.");
    }

    /// <summary>
    /// US-016 (AJAX): re-autentica con la contraseña y revela. El valor solo viaja en esta respuesta JSON,
    /// que no se guarda en caché; el script lo muestra 30 segundos y lo borra (RN-084).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Reveal(Guid id, [FromBody] RevealBody body, CancellationToken ct)
    {
        await api.SendAsync(HttpMethod.Post, "auth/reauthenticate", new { password = body.Password }, ct);
        var revealed = await api.PostAsync<RevealedValue>($"objects/{id}/reveal", new { }, ct);
        return Json(new { value = revealed.Value, expiresInSeconds = revealed.ExpiresInSeconds });
    }

    [HttpPost]
    public async Task<IActionResult> Download(Guid id, DownloadForm form, CancellationToken ct)
    {
        if (!Valid()) return Back(id);
        await api.SendAsync(HttpMethod.Post, "auth/reauthenticate", new { password = form.Password }, ct);
        var part = form.Part == "KeyMaterial" ? "KeyMaterial" : "PrivateKeyPfx";
        var (content, contentType, fileName) = await api.DownloadAsync($"objects/{id}/download?part={part}",
            new { downloadPassword = NullIfEmpty(form.FilePassword) }, ct);
        return File(content, contentType, fileName);
    }

    /// <summary>Certificado público: no es un valor sensible y basta con poder consultar el objeto (IMP-42).</summary>
    [HttpPost]
    public async Task<IActionResult> PublicCertificate(Guid id, CancellationToken ct)
    {
        var (content, contentType, fileName) = await api.DownloadAsync($"objects/{id}/download?part=PublicCertificate", new { }, ct);
        return File(content, contentType, fileName);
    }

    private bool Valid()
    {
        if (ModelState.IsValid) return true;
        TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
        return false;
    }

    private RedirectToActionResult Back(Guid id) => RedirectToAction(nameof(Details), new { id });

    private RedirectToActionResult Done(Guid id, string message)
    {
        TempData["Info"] = message;
        return Back(id);
    }

    private async Task<ObjectFormModel> FormModelAsync(ObjectForm form, CancellationToken ct) => new()
    {
        Form = form,
        Areas = await api.GetAsync<List<AreaView>>("areas", ct),
        Subtypes = await api.GetAsync<List<SubtypeView>>("catalog/subtypes", ct),
    };

    internal static Dictionary<string, string>? ParseAttributes(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var index = line.IndexOf('=', StringComparison.Ordinal);
            if (index > 0) result[line[..index].Trim()] = line[(index + 1)..].Trim();
        }
        return result;
    }

    internal static string FormatAttributes(IReadOnlyDictionary<string, string> attributes) =>
        string.Join('\n', attributes.Select(kv => $"{kv.Key}={kv.Value}"));

    private static async Task<string?> ReadFileAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > 64 * 1024) throw new ApiException(System.Net.HttpStatusCode.BadRequest, "FILE_TOO_LARGE", "El archivo supera 64 KB.", null, null);
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        try
        {
            return Convert.ToBase64String(bytes);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string? ToUtc(DateTime? local) =>
        local is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Local).ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : null;
}
