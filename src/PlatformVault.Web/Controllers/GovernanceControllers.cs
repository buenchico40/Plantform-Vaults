using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using PlatformVault.Web.Api;

namespace PlatformVault.Web.Controllers;

public sealed class RequestForm
{
    public Guid ObjectId { get; set; }
    [Required] public string Action { get; set; } = "Reveal";
    [Required(ErrorMessage = "La justificación es obligatoria."), MinLength(20, ErrorMessage = "Mínimo 20 caracteres."), MaxLength(1000)]
    public string Justification { get; set; } = "";
    public DateTime? RequestedStartAt { get; set; }
    [Range(1, 480)] public int RequestedDurationMinutes { get; set; } = 60;
}

public sealed class DecisionForm
{
    [Required] public string Decision { get; set; } = "Approved";
    [MaxLength(1000)] public string? Comment { get; set; }
    [Required(ErrorMessage = "Confirme su contraseña."), MaxLength(256), DataType(DataType.Password)] public string Password { get; set; } = "";
}

public sealed class RequestsModel
{
    public string Scope { get; set; } = "Mine";
    public Paged<AccessRequestView> Requests { get; set; } = new();
    public List<TemporaryAccessView> Accesses { get; set; } = [];
}

public sealed class AccessController(PlatformApi api) : Controller
{
    public async Task<IActionResult> Index(string scope = "Mine", CancellationToken ct = default)
    {
        scope = scope is "PendingForMe" or "All" ? scope : "Mine";
        return View(new RequestsModel
        {
            Scope = scope,
            Requests = await api.GetAsync<Paged<AccessRequestView>>($"access-requests?scope={scope}&pageSize=100", ct),
            Accesses = await api.GetAsync<List<TemporaryAccessView>>("temporary-access?onlyCurrent=false", ct),
        });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct) => View(await api.GetAsync<AccessRequestView>($"access-requests/{id}", ct));

    [HttpPost]
    public async Task<IActionResult> Create(RequestForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
            return RedirectToAction("Details", "Objects", new { id = form.ObjectId });
        }
        var created = await api.PostAsync<AccessRequestView>("access-requests", new
        {
            objectIds = new[] { form.ObjectId },
            action = form.Action,
            justification = form.Justification,
            requestedStartAt = form.RequestedStartAt is { } s ? DateTime.SpecifyKind(s, DateTimeKind.Local).ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : null,
            requestedDurationMinutes = form.RequestedDurationMinutes,
        }, ct);
        TempData["Info"] = $"Solicitud {created.Code} enviada a aprobación.";
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Decide(Guid id, DecisionForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Confirme su contraseña para decidir.";
            return RedirectToAction(nameof(Details), new { id });
        }
        await api.SendAsync(HttpMethod.Post, "auth/reauthenticate", new { password = form.Password }, ct);
        await api.PostAsync<AccessRequestView>($"access-requests/{id}/approvals", new { decision = form.Decision, comment = form.Comment }, ct);
        TempData["Info"] = form.Decision == "Approved" ? "Solicitud aprobada." : "Solicitud rechazada.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await api.SendAsync(HttpMethod.Post, $"access-requests/{id}/cancel", null, ct);
        TempData["Info"] = "Solicitud cancelada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Revoke(Guid id, [Required, MaxLength(300)] string reason, CancellationToken ct)
    {
        await api.SendAsync(HttpMethod.Post, $"temporary-access/{id}/revoke", new { reason }, ct);
        TempData["Info"] = "Acceso temporal revocado.";
        return RedirectToAction(nameof(Index));
    }
}

public sealed class GroupsController(PlatformApi api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await api.GetAsync<Paged<GroupSummary>>("groups?pageSize=200", ct));

    public async Task<IActionResult> Details(Guid id, CancellationToken ct) => View(await api.GetAsync<GroupDetail>($"groups/{id}", ct));

    [HttpPost]
    public async Task<IActionResult> Create([Required, MaxLength(150)] string name, [MaxLength(500)] string? description, Guid responsibleUserId,
        CancellationToken ct)
    {
        var created = await api.PostAsync<GroupSummary>("groups", new { name, description, responsibleUserId }, ct);
        TempData["Info"] = $"Grupo {created.Code} creado.";
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Update(Guid id, [Required, MaxLength(150)] string name, [MaxLength(500)] string? description, bool isActive,
        CancellationToken ct)
    {
        await api.PatchAsync<object>($"groups/{id}", new { name, description, status = isActive ? "Activo" : "Inactivo" }, null, ct);
        TempData["Info"] = "Grupo actualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> AddMember(Guid id, Guid userId, bool isResponsible, CancellationToken ct)
    {
        await api.SendAsync(HttpMethod.Post, $"groups/{id}/members", new { userId, isResponsible }, ct);
        TempData["Info"] = "Miembro agregado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        await api.SendAsync(HttpMethod.Delete, $"groups/{id}/members/{userId}", null, ct);
        TempData["Info"] = "Miembro retirado. Se revocaron los accesos obtenidos por esa pertenencia.";
        return RedirectToAction(nameof(Details), new { id });
    }
}

public sealed class AlertsController(PlatformApi api) : Controller
{
    public async Task<IActionResult> Index(bool onlyOpen = true, CancellationToken ct = default)
    {
        ViewBag.OnlyOpen = onlyOpen;
        return View(await api.GetAsync<Paged<AlertView>>($"alerts?onlyOpen={onlyOpen.ToString().ToLowerInvariant()}&pageSize=200", ct));
    }

    [HttpPost]
    public async Task<IActionResult> Acknowledge(Guid id, [Required, MaxLength(500)] string comment, CancellationToken ct)
    {
        await api.SendAsync(HttpMethod.Post, $"alerts/{id}/acknowledge", new { comment }, ct);
        TempData["Info"] = "Alerta reconocida. El escalamiento se detiene; la alerta se resuelve al renovar o desactivar el objeto.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Policies(CancellationToken ct) => View(await api.GetAsync<List<ExpirationPolicyView>>("expiration-policies", ct));

    [HttpPost]
    public async Task<IActionResult> SavePolicy(Guid? id, [Required, MaxLength(150)] string name, string? appliesToType, string? appliesToCriticality,
        [Required] string thresholds, bool isActive, CancellationToken ct)
    {
        var days = thresholds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => int.TryParse(d, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1).ToList();
        var body = new
        {
            name,
            appliesToType = string.IsNullOrEmpty(appliesToType) ? null : appliesToType,
            appliesToCriticality = string.IsNullOrEmpty(appliesToCriticality) ? null : appliesToCriticality,
            thresholdDays = days,
            isActive,
        };
        if (id is { } policyId)
            await api.SendAsync(HttpMethod.Put, $"expiration-policies/{policyId}", body, ct);
        else
            await api.SendAsync(HttpMethod.Post, "expiration-policies", body, ct);
        TempData["Info"] = "Política guardada.";
        return RedirectToAction(nameof(Policies));
    }
}

public sealed class AuditController(PlatformApi api) : Controller
{
    public async Task<IActionResult> Index(string? action, string? resourceId, DateTime? from, DateTime? to, int page = 1, CancellationToken ct = default)
    {
        var query = new List<string> { $"page={Math.Max(1, page)}", "pageSize=100" };
        if (!string.IsNullOrWhiteSpace(action)) query.Add("action=" + Uri.EscapeDataString(action));
        if (!string.IsNullOrWhiteSpace(resourceId)) query.Add("resourceId=" + Uri.EscapeDataString(resourceId));
        if (from is { } f) query.Add("from=" + Uri.EscapeDataString(f.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)));
        if (to is { } t) query.Add("to=" + Uri.EscapeDataString(t.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)));
        ViewBag.Action = action;
        ViewBag.ResourceId = resourceId;
        return View(await api.GetAsync<Paged<AuditEventView>>("audit-events?" + string.Join('&', query), ct));
    }

    [HttpPost]
    public async Task<IActionResult> Verify(CancellationToken ct)
    {
        var status = await api.PostAsync<AuditIntegrityStatus>("audit-events/integrity", null, ct);
        TempData[status.IsIntact ? "Info" : "Error"] = status.IsIntact
            ? $"Cadena de auditoría íntegra: {status.EventsVerified} eventos verificados."
            : $"CADENA ROTA en la secuencia {status.FirstBrokenSequence}. Se notificó a Seguridad.";
        return RedirectToAction(nameof(Index));
    }
}

public sealed class UsersController(PlatformApi api) : Controller
{
    public async Task<IActionResult> Index(string? text, CancellationToken ct)
    {
        ViewBag.Text = text;
        ViewBag.Areas = await api.GetAsync<List<AreaView>>("areas", ct);
        var q = string.IsNullOrWhiteSpace(text) ? string.Empty : "&q=" + Uri.EscapeDataString(text);
        return View(await api.GetAsync<Paged<UserView>>("users?pageSize=200" + q, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([Required, MaxLength(100)] string userName, [Required, MaxLength(200)] string displayName,
        [MaxLength(256)] string? email, Guid? areaId, List<string>? roles, CancellationToken ct)
    {
        var credential = await api.PostAsync<TemporaryCredential>("users", new { userName, displayName, email, areaId, roles = roles ?? [] }, ct);
        return View("Credential", (userName, credential.TemporaryPassword));
    }

    [HttpPost]
    public async Task<IActionResult> Update(Guid id, [Required, MaxLength(200)] string displayName, [MaxLength(256)] string? email, Guid? areaId,
        Guid? managerUserId, bool isActive, CancellationToken ct)
    {
        await api.SendAsync(HttpMethod.Put, $"users/{id}", new { displayName, email, areaId, managerUserId, isActive }, ct);
        TempData["Info"] = "Usuario actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Roles(Guid id, List<string>? roles, CancellationToken ct)
    {
        var user = await api.GetAsync<UserView>($"users/{id}", ct);
        var target = roles ?? [];
        foreach (var role in user.Roles.Except(target))
            await api.SendAsync(HttpMethod.Delete, $"users/{id}/roles/{Uri.EscapeDataString(role)}", null, ct);
        foreach (var role in target.Except(user.Roles))
            await api.SendAsync(HttpMethod.Post, $"users/{id}/roles", new { role }, ct);
        TempData["Info"] = "Roles actualizados. Las sesiones del usuario se cerraron.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(Guid id, string userName, CancellationToken ct)
    {
        var credential = await api.PostAsync<TemporaryCredential>($"users/{id}/reset-password", null, ct);
        return View("Credential", (userName, credential.TemporaryPassword));
    }

    [HttpPost]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
    {
        await api.SendAsync(HttpMethod.Post, $"users/{id}/unlock", null, ct);
        TempData["Info"] = "Usuario desbloqueado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CreateArea([Required, MaxLength(30)] string code, [Required, MaxLength(150)] string name, CancellationToken ct)
    {
        await api.SendAsync(HttpMethod.Post, "areas", new { code, name }, ct);
        TempData["Info"] = "Área creada.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Búsqueda de usuarios activos para los selectores (solo identificador y nombre).</summary>
    [HttpGet]
    public async Task<IActionResult> Lookup(string? text, CancellationToken ct)
    {
        var q = string.IsNullOrWhiteSpace(text) ? string.Empty : "?text=" + Uri.EscapeDataString(text);
        var users = await api.GetAsync<List<UserLookup>>("users/lookup" + q, ct);
        return Json(users.Select(u => new { id = u.Id, text = $"{u.DisplayName} ({u.UserName})" }));
    }
}
