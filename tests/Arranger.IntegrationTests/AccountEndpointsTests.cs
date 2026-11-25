using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts;
using NUnit.Framework;

namespace Faemiyah.Arranger.IntegrationTests;

[TestFixture]
public sealed class AccountEndpointsTests
{
    private ArrangerApp _app = null!;

    [OneTimeSetUp]
    public void StartServer()
    {
        _app = new ArrangerApp();
    }

    [OneTimeTearDown]
    public async Task StopServerAsync()
    {
        await _app.DisposeAsync();
    }

    [Test]
    public async Task Logout_SignsOutAndReturns()
    {
        using var http = _app.CreateHttpClient(await _app.RegisterAsync("Logout"));
        // The home page is not prerendered, so take the token from a server-rendered form for signed-in users.
        var token = await ArrangerApp.GetAntiforgeryTokenAsync(http, "Account/Manage");

        var response = await PostLogoutAsync(http, token, "settings");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/settings");
        (await http.GetAsync(ApiRoutes.MySettings)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Logout_WithoutAntiforgeryToken_KeepsTheSignIn()
    {
        using var http = _app.CreateHttpClient(await _app.RegisterAsync("NoToken"));

        var response = await PostLogoutAsync(http, null, string.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await http.GetAsync(ApiRoutes.MySettings)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> PostLogoutAsync(HttpClient http, string? token, string returnUrl)
    {
        var fields = new List<KeyValuePair<string, string>> { KeyValuePair.Create("ReturnUrl", returnUrl) };
        if (token != null)
        {
            fields.Add(KeyValuePair.Create("__RequestVerificationToken", token));
        }

        using var form = new FormUrlEncodedContent(fields);
        return await http.PostAsync("Account/Logout", form);
    }
}
