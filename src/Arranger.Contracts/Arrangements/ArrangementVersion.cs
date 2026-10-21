using System;
using System.Collections.Generic;
using System.Linq;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Orders snapshots of one arrangement.
/// </summary>
/// <remarks>
/// <see cref="Epoch"/> changes whenever the arrangement is loaded anew on the server, and <see cref="Sequence"/>
/// counts the changes within one epoch.
/// </remarks>
/// <param name="Epoch">The server-side activation the snapshot comes from.</param>
/// <param name="Sequence">The change counter within the epoch.</param>
public sealed record ArrangementVersion(Guid Epoch, long Sequence)
{
    /// <summary>
    /// Whether a snapshot with this version should replace one with the <paramref name="current"/> version.
    /// </summary>
    /// <remarks>
    /// A different epoch always wins: the server reloaded the arrangement from the database, and a snapshot is
    /// always complete.
    /// </remarks>
    /// <param name="current">The version currently shown, or <b>null</b> for none.</param>
    /// <returns><b>true</b> when this version should be applied.</returns>
    public bool Supersedes(ArrangementVersion? current)
    {
        return current == null || current.Epoch != Epoch || Sequence > current.Sequence;
    }
}
