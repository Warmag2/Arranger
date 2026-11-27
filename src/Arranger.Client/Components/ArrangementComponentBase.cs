using System;
using System.Net.Http;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Arrangements;
using Microsoft.AspNetCore.Components;

namespace Faemiyah.Arranger.Client.Components;

/// <summary>
/// The inputs shared by the components that show and change part of an arrangement.
/// </summary>
public abstract class ArrangementComponentBase : ComponentBase
{
    /// <summary>
    /// Whether a command is in progress.
    /// </summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>
    /// Runs a command: the method, the path under the arrangement or <b>null</b> for the arrangement itself, and the
    /// body. Returns whether it succeeded.
    /// </summary>
    [Parameter, EditorRequired]
    public Func<HttpMethod, string?, object?, Task<bool>> Run { get; set; } = null!;

    /// <summary>
    /// The arrangement.
    /// </summary>
    [Parameter, EditorRequired]
    public ArrangementSnapshot Snapshot { get; set; } = null!;

    /// <summary>
    /// The viewer's user ID.
    /// </summary>
    [Parameter, EditorRequired]
    public Guid UserId { get; set; }

    /// <summary>
    /// Whether the viewer is an admin member.
    /// </summary>
    protected bool IsAdmin => Me?.IsAdmin == true;

    /// <summary>
    /// The viewer as a member, or <b>null</b> when they are not one.
    /// </summary>
    protected ArrangementMemberSnapshot? Me => Snapshot.FindMember(UserId);
}
