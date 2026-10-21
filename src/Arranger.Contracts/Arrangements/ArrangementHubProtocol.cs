namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// The address and client-callable methods of the arrangement hub.
/// </summary>
public static class ArrangementHubProtocol
{
    /// <summary>
    /// The hub path, relative to the application base.
    /// </summary>
    public const string Path = "hubs/arrangement";

    /// <summary>
    /// <c>Subscribe(Guid arrangementId)</c>: start receiving <see cref="IArrangementClient"/> messages for an
    /// arrangement. Returns its current snapshot, or <b>null</b> when it does not exist.
    /// </summary>
    public const string Subscribe = "Subscribe";
}
