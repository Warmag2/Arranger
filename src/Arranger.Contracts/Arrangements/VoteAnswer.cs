namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// A member's answer to a time option. Zero is not a valid answer, so a missing value is caught.
/// </summary>
public enum VoteAnswer
{
    /// <summary>
    /// The time suits the member.
    /// </summary>
    Yes = 1,

    /// <summary>
    /// The time suits the member if needed.
    /// </summary>
    Maybe = 2,

    /// <summary>
    /// The time does not suit the member.
    /// </summary>
    No = 3,
}
