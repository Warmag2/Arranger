namespace Faemiyah.Arranger.Grains.Abstractions;

/// <summary>
/// Why a grain command failed.
/// </summary>
public enum CommandError
{
    /// <summary>
    /// The command succeeded.
    /// </summary>
    None,

    /// <summary>
    /// The target does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// The caller is not a member of the arrangement.
    /// </summary>
    NotMember,

    /// <summary>
    /// The caller lacks the rights for the command.
    /// </summary>
    Forbidden,

    /// <summary>
    /// The command input is invalid.
    /// </summary>
    Invalid,

    /// <summary>
    /// The command conflicts with the current state.
    /// </summary>
    Conflict,
}
