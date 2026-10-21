namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// A tab of the arrangement page. The lower-case name is its URL segment, e.g. <c>/{id}/expenses</c>.
/// </summary>
public enum ArrangementTab
{
    /// <summary>
    /// The scheduling poll.
    /// </summary>
    Schedule = 1,

    /// <summary>
    /// The shared expenses.
    /// </summary>
    Expenses = 2,

    /// <summary>
    /// The members, the link and the admin controls.
    /// </summary>
    Settings = 3,
}
