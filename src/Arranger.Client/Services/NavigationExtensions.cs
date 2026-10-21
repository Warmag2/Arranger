using System;
using Microsoft.AspNetCore.Components;

namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// Navigation helpers.
/// </summary>
public static class NavigationExtensions
{
    /// <summary>
    /// Leave for the login page, which brings the user back to the current page. Used when the sign-in was lost.
    /// </summary>
    /// <param name="navigation">The navigation manager.</param>
    public static void NavigateToLogin(this NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        var returnUrl = navigation.ToBaseRelativePath(navigation.Uri);
        navigation.NavigateTo($"Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
    }
}
