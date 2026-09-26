using Microsoft.AspNetCore.Hosting;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PlatformVault.Web.Controllers;

namespace PlatformVault.Web.Tests;

public sealed class WebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Api:Key", new string('k', 40));
        builder.UseSetting("Api:BaseUrl", "http://127.0.0.1:1/");
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "pv-web-tests-keys"));
        builder.UseEnvironment("Development");
    }
}

public sealed class WebSecurityTests(WebFactory factory) : IClassFixture<WebFactory>
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    [Fact]
    public async Task Anonymous_users_are_redirected_to_login()
    {
        var response = await Client().GetAsync(new Uri("/Objects", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_page_sends_security_headers_and_hides_api_key()
    {
        var response = await Client().GetAsync(new Uri("/Account/Login", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self' 'nonce-", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.DoesNotContain(new string('k', 40), html, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_post_without_csrf_token_is_rejected()
    {
        var response = await Client().PostAsync(new Uri("/Account/Login", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["UserName"] = "a", ["Password"] = "b" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("targetSystem=srv01\naccountName=svc", 2)]
    [InlineData("sin-igual\n=vacio", 0)]
    [InlineData("", -1)]
    public void Attributes_are_parsed_from_key_value_lines(string text, int expected)
    {
        var parsed = ObjectsController.ParseAttributes(text);
        Assert.Equal(expected, parsed?.Count ?? -1);
    }
}
