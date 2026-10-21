using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Grains;

namespace Faemiyah.Arranger.IntegrationTests;

/// <summary>
/// Remembers every notification, in order.
/// </summary>
public sealed class RecordingNotifier : IArrangementNotifier
{
    private readonly ConcurrentQueue<(Guid ArrangementId, ArrangementSnapshot? Snapshot)> _notifications = new();

    /// <summary>
    /// The notifications about one arrangement, in order. <b>null</b> stands for deletion.
    /// </summary>
    /// <param name="arrangementId">The public ID of the arrangement.</param>
    /// <returns>The notifications.</returns>
    public IReadOnlyList<ArrangementSnapshot?> For(Guid arrangementId)
    {
        return _notifications.Where(n => n.ArrangementId == arrangementId).Select(n => n.Snapshot).ToList();
    }

    /// <inheritdoc />
    public Task ChangedAsync(ArrangementSnapshot snapshot)
    {
        _notifications.Enqueue((snapshot.Id, snapshot));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeletedAsync(Guid arrangementId)
    {
        _notifications.Enqueue((arrangementId, null));
        return Task.CompletedTask;
    }
}
