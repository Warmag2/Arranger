using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Faemiyah.Arranger.Server.Authentication;

/// <summary>
/// Revalidates the security stamp of authentication cookies, and answers API and hub requests with status codes
/// instead of login redirects.
/// </summary>
public sealed class ArrangerCookieEvents : CookieAuthenticationEvents
{
    /// <summary>
    /// How old a cookie may get before its security stamp is checked against the database again.
    /// </summary>
    public static readonly TimeSpan ValidationInterval = TimeSpan.FromMinutes(5);

    private readonly AccountService _accountService;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrangerCookieEvents"/> class.
    /// </summary>
    /// <param name="accountService">The account service.</param>
    /// <param name="timeProvider">The time provider.</param>
    public ArrangerCookieEvents(AccountService accountService, TimeProvider timeProvider)
    {
        _accountService = accountService;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var issued = context.Properties.IssuedUtc;
        if (issued.HasValue && _timeProvider.GetUtcNow() - issued.Value < ValidationInterval)
        {
            return;
        }

        var principal = context.Principal;
        var userId = principal == null ? null : AccountService.GetUserId(principal);
        var stampValue = principal?.FindFirst(ArrangerClaimTypes.SecurityStamp)?.Value;

        if (userId.HasValue
            && Guid.TryParse(stampValue, out var stamp)
            && await _accountService.IsSecurityStampValidAsync(userId.Value, stamp, context.HttpContext.RequestAborted))
        {
            // Re-issue the cookie so the next check happens one interval from now.
            context.ShouldRenew = true;
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <inheritdoc />
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        return base.RedirectToLogin(context);
    }

    /// <inheritdoc />
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return base.RedirectToAccessDenied(context);
    }

    /// <summary>
    /// Whether the request goes to the HTTP API or a SignalR hub rather than a page.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><b>true</b> for API and hub requests.</returns>
    public static bool IsApiRequest(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase);
    }
}
