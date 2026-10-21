using System;
using System.Threading.Tasks;
using Faemiyah.Arranger.Server.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Faemiyah.Arranger.Server.Endpoints;

/// <summary>
/// Account endpoints which are not pages.
/// </summary>
public static class AccountEndpoints
{
    /// <summary>
    /// Map <c>POST /Account/Logout</c>. The form posting to it carries an antiforgery token.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint convention builder.</returns>
    public static IEndpointConventionBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/Account");
        group.MapPost("/Logout", LogoutAsync);
        return group;
    }

    private static async Task<RedirectHttpResult> LogoutAsync(HttpContext context, [FromForm] string? returnUrl)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.LocalRedirect("~/" + ReturnUrls.ToLocalPath(returnUrl));
    }
}
