using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Arranger.Contracts.Arrangements;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// A page's live view of one arrangement. Delivers the current snapshot on every (re)connect and every change after
/// that, and keeps reconnecting until disposed.
/// </summary>
/// <remarks>
/// Pushes are not stored anywhere, so anything sent while disconnected is lost. Instead, the snapshot is fetched again
/// on every reconnect and whenever the page becomes visible, which covers sleeping phones whose socket died silently.
/// </remarks>
public sealed class ArrangementLiveConnection : IAsyncDisposable
{
    private readonly NavigationManager _navigation;
    private readonly IJSRuntime _js;
    private readonly Guid _arrangementId;
    private readonly Func<ArrangementSnapshot?, Task> _onSnapshot;
    private readonly Func<Task> _onStateChanged;
    private readonly HubConnection _connection;
    private readonly CancellationTokenSource _stopping = new();
    private readonly DotNetObjectReference<ArrangementLiveConnection> _self;
    private IJSObjectReference? _visibilityWatch;
    private bool _connecting;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrangementLiveConnection"/> class. Call <see cref="StartAsync"/>
    /// to connect.
    /// </summary>
    /// <param name="navigation">The navigation manager.</param>
    /// <param name="js">The JS runtime.</param>
    /// <param name="arrangementId">The public ID of the arrangement.</param>
    /// <param name="onSnapshot">Receives each newer state, or <b>null</b> when the arrangement does not exist.</param>
    /// <param name="onStateChanged">Called when <see cref="IsLive"/> changes.</param>
    public ArrangementLiveConnection(
        NavigationManager navigation,
        IJSRuntime js,
        Guid arrangementId,
        Func<ArrangementSnapshot?, Task> onSnapshot,
        Func<Task> onStateChanged)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        _navigation = navigation;
        _js = js;
        _arrangementId = arrangementId;
        _onSnapshot = onSnapshot;
        _onStateChanged = onStateChanged;
        _self = DotNetObjectReference.Create(this);
        _connection = new HubConnectionBuilder()
            .WithUrl(navigation.ToAbsoluteUri(ArrangementHubProtocol.Path))
            .WithAutomaticReconnect(new ForeverRetryPolicy())
            .AddJsonProtocol(options => JsonConfiguration.ApplyDefaultJsonSerializerOptions(options.PayloadSerializerOptions))
            .Build();

        _connection.On<ArrangementSnapshot>(nameof(IArrangementClient.ArrangementChanged), snapshot =>
            snapshot.Id == _arrangementId ? _onSnapshot(snapshot) : Task.CompletedTask);
        _connection.On<Guid>(nameof(IArrangementClient.ArrangementDeleted), id =>
            id == _arrangementId ? _onSnapshot(null) : Task.CompletedTask);
        _connection.Reconnecting += _ => SetLiveAsync(false);
        _connection.Reconnected += _ => SubscribeAsync();
        _connection.Closed += async _ =>
        {
            await SetLiveAsync(false);
            await ConnectAsync();
        };
    }

    /// <summary>
    /// Gets a value indicating whether changes are currently arriving.
    /// </summary>
    public bool IsLive { get; private set; }

    /// <summary>
    /// Start watching page visibility and connect.
    /// </summary>
    /// <returns>A task that completes when the first connection attempt has finished.</returns>
    public async Task StartAsync()
    {
        await using var module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/visibility.js");
        _visibilityWatch = await module.InvokeAsync<IJSObjectReference>("watch", _self);
        await ConnectAsync();
    }

    /// <summary>
    /// Catch up after the page was hidden, e.g. on a phone that went to sleep.
    /// </summary>
    /// <returns>A task.</returns>
    [JSInvokable]
    public Task OnVisibleAsync()
    {
        return _connection.State == HubConnectionState.Connected ? SubscribeAsync() : ConnectAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        if (_visibilityWatch != null)
        {
            await _visibilityWatch.InvokeVoidAsync("dispose", CancellationToken.None);
            await _visibilityWatch.DisposeAsync();
        }

        await _connection.DisposeAsync();
        _self.Dispose();
        _stopping.Dispose();
    }

    private async Task ConnectAsync()
    {
        // Automatic reconnection covers connections that were up once. This covers the first start, and closes.
        if (_connecting || _stopping.IsCancellationRequested || _connection.State != HubConnectionState.Disconnected)
        {
            return;
        }

        _connecting = true;
        try
        {
            var attempts = 0;
            while (_connection.State == HubConnectionState.Disconnected)
            {
                await Task.Delay(ForeverRetryPolicy.GetDelay(attempts++), _stopping.Token);
                try
                {
                    await _connection.StartAsync(_stopping.Token);
                }
                catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _navigation.NavigateToLogin();
                    return;
                }
                catch (Exception exception) when (exception is HttpRequestException or AggregateException)
                {
                    // The server is unreachable. Try again after a delay.
                }
            }

            await SubscribeAsync();
        }
        catch (OperationCanceledException)
        {
            // Disposed while connecting.
        }
        finally
        {
            _connecting = false;
        }
    }

    private async Task SubscribeAsync()
    {
        try
        {
            var snapshot = await _connection.InvokeAsync<ArrangementSnapshot?>(ArrangementHubProtocol.Subscribe, _arrangementId, _stopping.Token);
            await SetLiveAsync(true);
            await _onSnapshot(snapshot);
        }
        catch (Exception exception) when (exception is HubException or IOException or InvalidOperationException or OperationCanceledException)
        {
            // The connection dropped mid-call. Reconnecting subscribes again.
            await SetLiveAsync(false);
        }
    }

    private async Task SetLiveAsync(bool isLive)
    {
        if (IsLive != isLive && !_stopping.IsCancellationRequested)
        {
            IsLive = isLive;
            await _onStateChanged();
        }
    }
}
