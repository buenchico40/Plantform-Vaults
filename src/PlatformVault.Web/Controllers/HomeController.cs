using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformVault.Web.Api;

namespace PlatformVault.Web.Controllers;

public sealed class HomeController(PlatformApi api) : Controller
{
    /// <summary>US-038: tablero operativo filtrado por el ámbito del usuario.</summary>
    public async Task<IActionResult> Index(CancellationToken ct) => View(await api.GetAsync<DashboardSummary>("dashboards/operational", ct));

    public IActionResult Denied() => View();

    [AllowAnonymous]
    public IActionResult Error() => View();
}
