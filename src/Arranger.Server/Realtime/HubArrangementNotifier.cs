using System;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Grains;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Faemiyah.Arranger.Server.Realtime;

/// <summary>
/// Sends grain notifications to the arrangement's SignalR group.
/// </summary>
public sealed class HubArrangementNotifier : IArrangementNotifier
{
    private readonly IHubContext<ArrangementHub, IArrangementClient> _hub;
    private readonly ILogger<HubArrangementNotifier> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HubArrangementNotifier"/> class.
    /// </summary>
    /// <param name="hub">The hub context.</param>
    /// <param name="logger">The logger.</param>
    public HubArrangementNotifier(IHubContext<ArrangementHub, IArrangementClient> hub, ILogger<HubArrangementNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task ChangedAsync(ArrangementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return SendAsync(snapshot.Id, client => client.ArrangementChanged(snapshot));
    }

    /// <inheritdoc />
    public Task DeletedAsync(Guid arrangementId)
    {
        return SendAsync(arrangementId, client => client.ArrangementDeleted(arrangementId));
    }

    private async Task SendAsync(Guid arrangementId, Func<IArrangementClient, Task> send)
    {
        try
        {
            await send(_hub.Clients.Group(ArrangementHub.GroupName(arrangementId)));
        }
        catch (Exception exception)
        {
            // The change is already saved. Clients resubscribe on reconnect, so a lost push only delays an update.
            _logger.LogWarning(exception, "Notifying the clients of arrangement {ArrangementId} failed.", arrangementId);
        }
    }
}
