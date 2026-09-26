using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using PlatformVault.Web.Api;
using PlatformVault.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ApiOptions>().Bind(builder.Configuration.GetSection(ApiOptions.Section))
    .Validate(o => o.Key.Length >= 32, "Api:Key es obligatoria (mínimo 32 caracteres) y se configura fuera del repositorio.")
    .ValidateOnStart();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ApiCredentialsHandler>();
builder.Services.AddHttpClient<PlatformApi>((sp, client) =>
    {
        var baseUrl = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiOptions>>().Value.BaseUrl.ToString();
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl + "api/v1/" : baseUrl + "/api/v1/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<ApiCredentialsHandler>();

// IMP-07: la cookie lleva el token de sesión de la API cifrado con Data Protection; nunca la API Key.
var keysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlatformVault", "web-keys");
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("PlatformVault.Web").PersistKeysToFileSystem(new DirectoryInfo(keysPath));
if (OperatingSystem.IsWindows())
    dataProtection.ProtectKeysWithDpapi();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "__Host-pv";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.Path = "/";
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Home/Denied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = false;
    });
builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "RequestVerificationToken";
    o.Cookie.Name = "__Host-pv-af";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddControllersWithViews(o =>
{
    // CSRF en todos los formularios y peticiones que modifican estado.
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.Filters.Add<ApiExceptionFilter>();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<PasswordChangeGateMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program
{
    /// <summary>Nonce aleatorio por petición para la CSP (script-src).</summary>
    internal static string NewNonce() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
}
