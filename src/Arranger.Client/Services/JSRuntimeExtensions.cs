using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// Browser dialog helpers.
/// </summary>
public static class JSRuntimeExtensions
{
    /// <summary>
    /// Ask the user to confirm an action with the browser's confirmation dialog.
    /// </summary>
    /// <param name="js">The JavaScript runtime.</param>
    /// <param name="message">The question to show.</param>
    /// <returns><b>true</b> when the user confirmed.</returns>
    public static ValueTask<bool> ConfirmAsync(this IJSRuntime js, string message)
    {
        ArgumentNullException.ThrowIfNull(js);
        return js.InvokeAsync<bool>("confirm", message);
    }
}
