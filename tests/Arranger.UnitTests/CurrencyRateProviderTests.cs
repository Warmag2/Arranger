using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Grains;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class CurrencyRateProviderTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task GetRates_ComputesCrossRatesFromThePivotTable()
    {
        var provider = NewProvider(out _, out _, Success("USD", 1m, ("EUR", 0.9m), ("JPY", 150m)));

        var rates = await provider.GetRatesAsync("EUR", ["JPY", "USD"]);

        rates["JPY"].Should().Be(166.66666667m);
        rates["USD"].Should().Be(1.11111111m);
    }

    [Test]
    public async Task GetRates_IgnoresUnknownCurrencyCodesFromTheUpstream()
    {
        var provider = NewProvider(out _, out _, Success("USD", 1m, ("EUR", 0.9m), ("ZZZ", 5m)));

        var rates = await provider.GetRatesAsync("USD", ["ZZZ", "EUR"]);

        rates.Keys.Should().BeEquivalentTo("EUR");
    }

    [Test]
    public async Task GetRates_KeepsTheLastKnownRatesWhenAFetchFails()
    {
        var handler = new StubHandler(call => call == 1
            ? Success("USD", 1m, ("EUR", 0.9m))
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var time = new MutableTimeProvider(Start);
        var provider = new CurrencyRateProvider(new StubHttpClientFactory(handler), time, NullLogger<CurrencyRateProvider>.Instance);
        await provider.GetRatesAsync("USD", ["EUR"]);
        time.Advance(TimeSpan.FromHours(13));

        var rates = await provider.GetRatesAsync("USD", ["EUR"]);

        handler.Calls.Should().Be(2);
        rates["EUR"].Should().Be(0.9m);
    }

    [Test]
    public async Task GetRates_LeavesOutCurrenciesTheUpstreamDoesNotCover()
    {
        var provider = NewProvider(out _, out _, Success("USD", 1m, ("EUR", 0.9m)));

        var rates = await provider.GetRatesAsync("EUR", ["JPY", "USD"]);

        rates.Keys.Should().BeEquivalentTo("USD");
    }

    [Test]
    public async Task GetRates_PropagatesCancellationByTheCaller()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHandler(_ =>
        {
            cancellation.Cancel();
            throw new TaskCanceledException();
        });
        var provider = new CurrencyRateProvider(new StubHttpClientFactory(handler), new MutableTimeProvider(Start), NullLogger<CurrencyRateProvider>.Instance);

        var fetch = () => provider.GetRatesAsync("USD", ["EUR"], cancellation.Token);

        await fetch.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task GetRates_RefetchesWhenTheCacheIsStale()
    {
        var provider = NewProvider(out var handler, out var time, Success("USD", 1m, ("EUR", 0.9m)));
        await provider.GetRatesAsync("USD", ["EUR"]);
        time.Advance(TimeSpan.FromHours(13));

        await provider.GetRatesAsync("USD", ["EUR"]);

        handler.Calls.Should().Be(2);
    }

    [Test]
    public async Task GetRates_ReturnsNothingForAnEmptyRequestWithoutFetching()
    {
        var provider = NewProvider(out var handler, out _, Success("USD", 1m, ("EUR", 0.9m)));

        var rates = await provider.GetRatesAsync("USD", []);

        rates.Should().BeEmpty();
        handler.Calls.Should().Be(0);
    }

    [Test]
    public async Task GetRates_ReturnsNothingWhenTheBaseCurrencyIsNotPriced()
    {
        var provider = NewProvider(out _, out _, Success("USD", 1m, ("EUR", 0.9m)));

        var rates = await provider.GetRatesAsync("JPY", ["EUR"]);

        rates.Should().BeEmpty();
    }

    [Test]
    public async Task GetRates_ServesTheCacheWhileFresh()
    {
        var provider = NewProvider(out var handler, out var time, Success("USD", 1m, ("EUR", 0.9m)));
        await provider.GetRatesAsync("USD", ["EUR"]);
        time.Advance(TimeSpan.FromHours(11));

        await provider.GetRatesAsync("USD", ["EUR"]);

        handler.Calls.Should().Be(1);
    }

    private static HttpResponseMessage Clone(HttpResponseMessage response)
    {
        var copy = new HttpResponseMessage(response.StatusCode);
        if (response.Content is { } content)
        {
            copy.Content = new StringContent(content.ReadAsStringAsync().GetAwaiter().GetResult(), Encoding.UTF8, "application/json");
        }

        return copy;
    }

    private static CurrencyRateProvider NewProvider(out StubHandler handler, out MutableTimeProvider time, HttpResponseMessage response)
    {
        handler = new StubHandler(_ => Clone(response));
        time = new MutableTimeProvider(Start);
        return new CurrencyRateProvider(new StubHttpClientFactory(handler), time, NullLogger<CurrencyRateProvider>.Instance);
    }

    private static HttpResponseMessage Success(string baseCode, decimal pivot, params (string Code, decimal Rate)[] rates)
    {
        var builder = new StringBuilder();
        builder.Append("{\"result\":\"success\",\"base_code\":\"").Append(baseCode).Append("\",\"rates\":{");
        builder.Append('"').Append(baseCode).Append("\":").Append(pivot.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var (code, rate) in rates)
        {
            builder.Append(",\"").Append(code).Append("\":").Append(rate.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        builder.Append("}}");
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(builder.ToString(), Encoding.UTF8, "application/json"),
        };
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public MutableTimeProvider(DateTimeOffset start)
        {
            _now = start;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpResponseMessage> _responder;

        public StubHandler(Func<int, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_responder(Calls));
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://open.er-api.com/") };
        }
    }
}
