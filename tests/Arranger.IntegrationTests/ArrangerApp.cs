using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;

namespace Faemiyah.Arranger.IntegrationTests;

/// <summary>
/// Runs the real server in memory, with its own database in the shared PostgreSQL container.
/// </summary>
public sealed partial class ArrangerApp : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>
    /// Configure the server. It starts on first use.
    /// </summary>
    public ArrangerApp()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseSetting("ConnectionStrings:Database", PostgreSqlFixture.BaseConnectionString);
            web.UseSetting("Sql:DefaultDbName", "arranger_" + Guid.NewGuid().ToString("N"));
            web.UseSetting("Orleans:SiloPort", FreePort());
            web.UseSetting("Orleans:GatewayPort", FreePort());
        });
    }

    /// <summary>
    /// The JSON options the server and client use.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    /// <summary>
    /// The in-memory server.
    /// </summary>
    public TestServer Server => _factory.Server;

    /// <summary>
    /// Create an HTTP client that keeps its cookies in <paramref name="cookies"/> and sends the API header.
    /// </summary>
    /// <param name="cookies">The cookies, for example from <see cref="RegisterAsync"/>.</param>
    /// <returns>The client.</returns>
    public HttpClient CreateHttpClient(CookieContainer cookies)
    {
        var http = _factory.CreateDefaultClient(new CookieContainerHandler(cookies));
        http.DefaultRequestHeaders.Add(ApiHeaders.RequestedWith, ApiHeaders.RequestedWithValue);
        return http;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _factory.DisposeAsync();
    }

    /// <summary>
    /// Check that a response succeeded, and read its JSON body.
    /// </summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="response">The response.</param>
    /// <returns>The body.</returns>
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    /// <summary>
    /// Register a user through the registration page, which also signs them in.
    /// </summary>
    /// <param name="userName">The username.</param>
    /// <returns>The sign-in cookies.</returns>
    public async Task<CookieContainer> RegisterAsync(string userName)
    {
        var cookies = new CookieContainer();
        using var http = CreateHttpClient(cookies);
        var page = await http.GetStringAsync("Account/Register");
        using var form = new FormUrlEncodedContent(new[]
        {
            KeyValuePair.Create("_handler", "register"),
            KeyValuePair.Create("__RequestVerificationToken", AntiforgeryToken().Match(page).Groups[1].Value),
            KeyValuePair.Create("Input.TimeZoneId", "Europe/Helsinki"),
            KeyValuePair.Create("Input.UserName", userName),
            KeyValuePair.Create("Input.Password", "correct horse battery staple"),
            KeyValuePair.Create("Input.ConfirmPassword", "correct horse battery staple"),
        });

        var response = await http.PostAsync("Account/Register", form);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        return cookies;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfiguration.ApplyDefaultJsonSerializerOptions(options);
        return options;
    }

    private static string FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port.ToString(CultureInfo.InvariantCulture);
    }
}
