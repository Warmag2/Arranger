using System;

namespace Faemiyah.Arranger.Contracts;

/// <summary>
/// The API and hub addresses shared by the server and its client, relative to the application base.
/// </summary>
/// <remarks>
/// The server maps them with a leading <c>/</c>. The sub-paths of an arrangement, such as <c>join</c>, are written
/// where the server maps them and where the client calls them.
/// </remarks>
public static class ApiRoutes
{
    /// <summary>
    /// The prefix of every API address.
    /// </summary>
    public const string Api = "api";

    /// <summary>
    /// The prefix of every SignalR hub address.
    /// </summary>
    public const string Hubs = "hubs";

    /// <summary>
    /// The arrangements of the signed-in user. One arrangement is at <see cref="Arrangement"/>.
    /// </summary>
    public const string Arrangements = $"{Api}/arrangements";

    /// <summary>
    /// The prefix of the signed-in user's own resources.
    /// </summary>
    public const string Me = $"{Api}/me";

    /// <summary>
    /// The signed-in user's picture.
    /// </summary>
    public const string MyPicture = $"{Me}/picture";

    /// <summary>
    /// The signed-in user's settings.
    /// </summary>
    public const string MySettings = $"{Me}/settings";

    /// <summary>
    /// The time zones users can choose from.
    /// </summary>
    public const string TimeZones = $"{Api}/time-zones";

    /// <summary>
    /// The users. A user's picture is at <see cref="UserPicture"/>.
    /// </summary>
    public const string Users = $"{Api}/users";

    /// <summary>
    /// The address of an arrangement.
    /// </summary>
    /// <param name="id">The arrangement ID.</param>
    /// <returns>The address.</returns>
    public static string Arrangement(Guid id)
    {
        return $"{Arrangements}/{id}";
    }

    /// <summary>
    /// The address of a version of a user's picture.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="version">The picture version.</param>
    /// <returns>The address.</returns>
    public static string UserPicture(Guid userId, uint version)
    {
        return $"{Users}/{userId}/picture?v={version}";
    }
}
