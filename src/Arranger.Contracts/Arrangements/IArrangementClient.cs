using System;
using System.Threading.Tasks;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// The messages the arrangement hub pushes to clients. Push only: clients change state through the API.
/// </summary>
/// <remarks>
/// Messages can be lost while a client is disconnected, so clients resubscribe after reconnecting, which returns the
/// current snapshot.
/// </remarks>
public interface IArrangementClient
{
    /// <summary>
    /// An arrangement changed. Apply only when <see cref="ArrangementVersion.Supersedes"/> the current version.
    /// </summary>
    /// <param name="snapshot">The whole arrangement after the change.</param>
    /// <returns>A task.</returns>
    Task ArrangementChanged(ArrangementSnapshot snapshot);

    /// <summary>
    /// An arrangement was deleted.
    /// </summary>
    /// <param name="arrangementId">The public ID of the arrangement.</param>
    /// <returns>A task.</returns>
    Task ArrangementDeleted(Guid arrangementId);
}
