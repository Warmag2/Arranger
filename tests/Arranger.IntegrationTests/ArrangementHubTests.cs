using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Arranger.Contracts.Arrangements;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Faemiyah.Arranger.IntegrationTests;

[TestFixture]
public sealed partial class ArrangementHubTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void StartServer()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseSetting("ConnectionStrings:Database", PostgreSqlFixture.BaseConnectionString);
            web.UseSetting("Sql:DefaultDbName", "arranger_" + Guid.NewGuid().ToString("N"));
            web.UseSetting("Orleans:SiloPort", FreePort());
            web.UseSetting("Orleans:GatewayPort", FreePort());
        });
    }

    [OneTimeTearDown]
    public async Task StopServerAsync()
    {
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Subscriber_ReceivesChangesAndDeletion()
    {
        var alice = await RegisterAsync("Alice");
        var bob = await RegisterAsync("Bob");
        using var aliceHttp = CreateHttpClient(alice);
        using var bobHttp = CreateHttpClient(bob);
        var created = await aliceHttp.PostAsJsonAsync("api/arrangements", new CreateArrangementRequest("Hub test", string.Empty), JsonOptions);
        var arrangement = (await created.Content.ReadFromJsonAsync<ArrangementSnapshot>(JsonOptions))!;

        var pushes = Channel.CreateUnbounded<ArrangementSnapshot?>();
        await using var connection = CreateHubConnection(alice);
        connection.On<ArrangementSnapshot>(nameof(IArrangementClient.ArrangementChanged), s => pushes.Writer.TryWrite(s));
        connection.On<Guid>(nameof(IArrangementClient.ArrangementDeleted), _ => pushes.Writer.TryWrite(null));
        await connection.StartAsync();
        var subscribed = await connection.InvokeAsync<ArrangementSnapshot?>(ArrangementHubProtocol.Subscribe, arrangement.Id);
        subscribed!.Members.Should().ContainSingle();

        (await bobHttp.PostAsync($"api/arrangements/{arrangement.Id}/join", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var joined = await ReadAsync(pushes);
        joined!.Members.Should().HaveCount(2);
        joined.Version.Supersedes(subscribed.Version).Should().BeTrue();

        (await aliceHttp.DeleteAsync($"api/arrangements/{arrangement.Id}")).IsSuccessStatusCode.Should().BeTrue();
        (await ReadAsync(pushes)).Should().BeNull();
    }

    [Test]
    public async Task Subscribe_ToMissingArrangement_ReturnsNull()
    {
        await using var connection = CreateHubConnection(await RegisterAsync("Carol"));
        await connection.StartAsync();

        (await connection.InvokeAsync<ArrangementSnapshot?>(ArrangementHubProtocol.Subscribe, Guid.NewGuid())).Should().BeNull();
    }

    [Test]
    public async Task Connect_WithoutSignIn_IsUnauthorized()
    {
        await using var connection = CreateHubConnection(new CookieContainer());

        var start = async () => await connection.StartAsync();

        (await start.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

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

    private static async Task<ArrangementSnapshot?> ReadAsync(Channel<ArrangementSnapshot?> pushes)
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        return await pushes.Reader.ReadAsync(cancellation.Token);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();

    private async Task<CookieContainer> RegisterAsync(string userName)
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

    private HttpClient CreateHttpClient(CookieContainer cookies)
    {
        var http = _factory.CreateDefaultClient(new CookieContainerHandler(cookies));
        http.DefaultRequestHeaders.Add(ApiHeaders.RequestedWith, ApiHeaders.RequestedWithValue);
        return http;
    }

    private HubConnection CreateHubConnection(CookieContainer cookies)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, ArrangementHubProtocol.Path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => new CookieContainerHandler(cookies) { InnerHandler = _factory.Server.CreateHandler() };
            })
            .AddJsonProtocol(options => JsonConfiguration.ApplyDefaultJsonSerializerOptions(options.PayloadSerializerOptions))
            .Build();
    }
}
