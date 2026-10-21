using System;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Grains.Abstractions;
using Faemiyah.Arranger.Server.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Orleans;

namespace Faemiyah.Arranger.Server.Realtime;

/// <summary>
/// Pushes arrangement changes to the clients watching them. One group per arrangement.
/// </summary>
[Authorize]
public sealed class ArrangementHub : Hub<IArrangementClient>
{
    private readonly IGrainFactory _grains;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrangementHub"/> class.
    /// </summary>
    /// <param name="grains">The grain factory.</param>
    public ArrangementHub(IGrainFactory grains)
    {
        _grains = grains;
    }

    /// <summary>
    /// The SignalR group of an arrangement.
    /// </summary>
    /// <param name="arrangementId">The public ID of the arrangement.</param>
    /// <returns>The group name.</returns>
    public static string GroupName(Guid arrangementId)
    {
        return arrangementId.ToString("N");
    }

    /// <summary>
    /// See <see cref="ArrangementHubProtocol.Subscribe"/>.
    /// </summary>
    /// <param name="arrangementId">The public ID of the arrangement.</param>
    /// <returns>The current snapshot, or <b>null</b> when the arrangement does not exist.</returns>
    public async Task<ArrangementSnapshot?> Subscribe(Guid arrangementId)
    {
        // Join before reading, so that no change can fall between the snapshot and the first push.
        var group = GroupName(arrangementId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        var result = await _grains.GetGrain<IArrangementGrain>(arrangementId).GetSnapshotAsync(Context.User!.GetRequiredUserId());
        if (!result.IsSuccess)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }

        return result.Snapshot;
    }
}
