using System;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Arrangements;

namespace Faemiyah.Arranger.Grains;

/// <summary>
/// Tells the clients watching an arrangement that it changed.
/// </summary>
/// <remarks>
/// Implementations never throw: a lost notification only delays an update until the client resubscribes.
/// </remarks>
public interface IArrangementNotifier
{
    /// <summary>
    /// Send the new state of an arrangement.
    /// </summary>
    /// <param name="snapshot">The arrangement after the change.</param>
    /// <returns>A task.</returns>
    Task ChangedAsync(ArrangementSnapshot snapshot);

    /// <summary>
    /// Announce that an arrangement was deleted.
    /// </summary>
    /// <param name="arrangementId">The public ID of the arrangement.</param>
    /// <returns>A task.</returns>
    Task DeletedAsync(Guid arrangementId);
}
