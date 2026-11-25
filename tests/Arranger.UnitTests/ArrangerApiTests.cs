using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Client.Services;
using Faemiyah.Arranger.Contracts.Users;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ArrangerApiTests
{
    private const string Host = "https://arranger.test/";
    private const string Path = "api/me/settings";

    [Test]
    public async Task SendAsync_ConnectionLost_ReturnsError()
    {
        var api = Create(_ => throw new HttpRequestException("Failed to fetch"));

        var error = await api.SendAsync(HttpMethod.Delete, Path);

        error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task SendAsyncOfT_ConnectionLost_ReturnsError()
    {
        var api = Create(_ => throw new HttpRequestException("Failed to fetch"));

        var result = await api.GetAsync<UserSettings>(Path);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task SendAsyncOfT_NoContent_ReturnsError()
    {
        var api = Create(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        var result = await api.GetAsync<UserSettings>(Path);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task SendAsyncOfT_TimedOut_ReturnsError()
    {
        var api = Create(
            async token =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            },
            TimeSpan.FromMilliseconds(50));

        var result = await api.GetAsync<UserSettings>(Path);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    private static ArrangerApi Create(Func<CancellationToken, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null)
    {
        var http = new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri(Host) };
        if (timeout is { } value)
        {
            http.Timeout = value;
        }

        return new ArrangerApi(http, new StubNavigationManager());
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;

        public StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _respond(cancellationToken);
        }
    }

    private sealed class StubNavigationManager : NavigationManager
    {
        public StubNavigationManager()
        {
            Initialize(Host, Host);
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }
}
