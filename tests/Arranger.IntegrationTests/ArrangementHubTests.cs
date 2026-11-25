using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Common;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Faemiyah.Arranger.IntegrationTests;

[TestFixture]
public sealed class ArrangementHubTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

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
    public async Task Api_BindsRateRouteValueAndBody()
    {
        using var http = _app.CreateHttpClient(await _app.RegisterAsync("Frank"));
        var arrangement = await CreateArrangementAsync(http);

        var rated = await http.PutAsJsonAsync($"{ApiRoutes.Arrangement(arrangement.Id)}/rates/SEK", new SetExchangeRateRequest(11.5m), ArrangerApp.JsonOptions);

        (await ArrangerApp.ReadAsync<ArrangementSnapshot>(rated)).ExchangeRates["SEK"].Should().Be(new ExchangeRateSnapshot(11.5m, 0));
    }

    [Test]
    public async Task Api_BindsVoteRouteValuesAndBody()
    {
        using var http = _app.CreateHttpClient(await _app.RegisterAsync("Grace"));
        var arrangement = await CreateArrangementAsync(http);
        var added = await http.PostAsJsonAsync($"{ApiRoutes.Arrangement(arrangement.Id)}/time-options", new AddTimeOptionRequest(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, true), ArrangerApp.JsonOptions);
        var optionId = (await ArrangerApp.ReadAsync<ArrangementSnapshot>(added)).TimeOptions.Single().Id;

        var voted = await http.PutAsJsonAsync($"{ApiRoutes.Arrangement(arrangement.Id)}/time-options/{optionId}/vote", new SetVoteRequest(VoteAnswer.Maybe), ArrangerApp.JsonOptions);

        (await ArrangerApp.ReadAsync<ArrangementSnapshot>(voted)).TimeOptions.Single().Votes.Single().Answer.Should().Be(VoteAnswer.Maybe);
    }

    [Test]
    public async Task Api_CreateReturnsLocation()
    {
        using var http = _app.CreateHttpClient(await _app.RegisterAsync("Heidi"));

        var created = await http.PostAsJsonAsync(ApiRoutes.Arrangements, new CreateArrangementRequest("Location test", string.Empty), ArrangerApp.JsonOptions);

        var id = (await ArrangerApp.ReadAsync<ArrangementSnapshot>(created)).Id;
        created.Headers.Location.Should().Be(new Uri("/" + ApiRoutes.Arrangement(id), UriKind.Relative));
    }

    [Test]
    public async Task Connect_FromAnotherOrigin_IsForbidden()
    {
        await using var connection = CreateHubConnection(await _app.RegisterAsync("Dave"), "https://sibling.localhost");

        var start = async () => await connection.StartAsync();

        (await start.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Connect_FromOwnOrigin_Succeeds()
    {
        await using var connection = CreateHubConnection(await _app.RegisterAsync("Erin"), _app.Server.BaseAddress.GetLeftPart(UriPartial.Authority));

        await connection.StartAsync();

        connection.State.Should().Be(HubConnectionState.Connected);
    }

    [Test]
    public async Task Connect_WithoutSignIn_IsUnauthorized()
    {
        await using var connection = CreateHubConnection(new CookieContainer());

        var start = async () => await connection.StartAsync();

        (await start.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Subscribe_ReturnsTheCurrentSnapshot()
    {
        var ivan = await _app.RegisterAsync("Ivan");
        using var http = _app.CreateHttpClient(ivan);
        var arrangement = await CreateArrangementAsync(http);
        await using var connection = await ConnectAsync(ivan, Channel.CreateUnbounded<ArrangementSnapshot?>());

        var subscribed = await connection.InvokeAsync<ArrangementSnapshot?>(ArrangementHubProtocol.Subscribe, arrangement.Id);

        subscribed!.Id.Should().Be(arrangement.Id);
        subscribed.Members.Should().ContainSingle();
    }

    [Test]
    public async Task Subscribe_ToMissingArrangement_ReturnsNull()
    {
        await using var connection = CreateHubConnection(await _app.RegisterAsync("Carol"));
        await connection.StartAsync();

        var subscribed = await connection.InvokeAsync<ArrangementSnapshot?>(ArrangementHubProtocol.Subscribe, Guid.NewGuid());

        subscribed.Should().BeNull();
    }

    [Test]
    public async Task Subscriber_ReceivesChanges()
    {
        var alice = await _app.RegisterAsync("Alice");
        using var aliceHttp = _app.CreateHttpClient(alice);
        using var bobHttp = _app.CreateHttpClient(await _app.RegisterAsync("Bob"));
        var arrangement = await CreateArrangementAsync(aliceHttp);
        var pushes = Channel.CreateUnbounded<ArrangementSnapshot?>();
        await using var connection = await ConnectAsync(alice, pushes);
        var subscribed = await connection.InvokeAsync<ArrangementSnapshot?>(ArrangementHubProtocol.Subscribe, arrangement.Id);

        (await bobHttp.PostAsync($"{ApiRoutes.Arrangement(arrangement.Id)}/join", null)).EnsureSuccessStatusCode();
        var joined = await ReadAsync(pushes);

        joined!.Members.Should().HaveCount(2);
        joined.Version.Supersedes(subscribed!.Version).Should().BeTrue();
    }

    [Test]
    public async Task Subscriber_ReceivesDeletion()
    {
        var judy = await _app.RegisterAsync("Judy");
        using var http = _app.CreateHttpClient(judy);
        var arrangement = await CreateArrangementAsync(http);
        var pushes = Channel.CreateUnbounded<ArrangementSnapshot?>();
        await using var connection = await ConnectAsync(judy, pushes);
        await connection.InvokeAsync<ArrangementSnapshot?>(ArrangementHubProtocol.Subscribe, arrangement.Id);

        (await http.DeleteAsync(ApiRoutes.Arrangement(arrangement.Id))).EnsureSuccessStatusCode();
        var deleted = await ReadAsync(pushes);

        deleted.Should().BeNull();
    }

    private static async Task<ArrangementSnapshot> CreateArrangementAsync(HttpClient http)
    {
        var created = await http.PostAsJsonAsync(ApiRoutes.Arrangements, new CreateArrangementRequest("Hub test", string.Empty), ArrangerApp.JsonOptions);
        return await ArrangerApp.ReadAsync<ArrangementSnapshot>(created);
    }

    private static async Task<ArrangementSnapshot?> ReadAsync(Channel<ArrangementSnapshot?> pushes)
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        return await pushes.Reader.ReadAsync(cancellation.Token);
    }

    private async Task<HubConnection> ConnectAsync(CookieContainer cookies, Channel<ArrangementSnapshot?> pushes)
    {
        var connection = CreateHubConnection(cookies);
        connection.On<ArrangementSnapshot>(nameof(IArrangementClient.ArrangementChanged), s => pushes.Writer.TryWrite(s));
        connection.On<Guid>(nameof(IArrangementClient.ArrangementDeleted), _ => pushes.Writer.TryWrite(null));
        await connection.StartAsync();
        return connection;
    }

    private HubConnection CreateHubConnection(CookieContainer cookies, string? origin = null)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_app.Server.BaseAddress, ArrangementHubProtocol.Path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => new CookieContainerHandler(cookies) { InnerHandler = _app.Server.CreateHandler() };
                if (origin != null)
                {
                    options.Headers["Origin"] = origin;
                }
            })
            .AddJsonProtocol(options => JsonConfiguration.ApplyDefaultJsonSerializerOptions(options.PayloadSerializerOptions))
            .Build();
    }
}
