using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Arranger.Grains.Abstractions;
using Faemiyah.Arranger.Server.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Faemiyah.Arranger.Server.Endpoints;

/// <summary>
/// Plumbing shared by the API endpoint groups.
/// </summary>
public static class ApiEndpointExtensions
{
    /// <summary>
    /// Map an API group: authenticated, and with <see cref="ApiHeaders.RequestedWith"/> required on writes.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="prefix">The route prefix.</param>
    /// <returns>The group.</returns>
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder endpoints, string prefix)
    {
        var group = endpoints.MapGroup(prefix);
        group.RequireAuthorization();
        group.AddEndpointFilter(RequireApiHeaderAsync);
        return group;
    }

    /// <summary>
    /// The authenticated user's public ID.
    /// </summary>
    /// <param name="principal">The principal of an authorized request.</param>
    /// <returns>The user ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the principal has no user ID.</exception>
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        return AccountService.GetUserId(principal) ?? throw new InvalidOperationException("The principal has no user ID.");
    }

    /// <summary>
    /// The HTTP response for a command result: the snapshot, no content, or a problem.
    /// </summary>
    /// <param name="result">The command result.</param>
    /// <returns>The response.</returns>
    public static IResult ToHttpResult(this CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            return result.Snapshot == null ? TypedResults.NoContent() : TypedResults.Ok(result.Snapshot);
        }

        var statusCode = result.Error switch
        {
            CommandError.NotFound => StatusCodes.Status404NotFound,
            CommandError.NotMember or CommandError.Forbidden => StatusCodes.Status403Forbidden,
            CommandError.Invalid => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status409Conflict,
        };

        return TypedResults.Problem(detail: result.Message, statusCode: statusCode);
    }

    private static async ValueTask<object?> RequireApiHeaderAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!HttpMethods.IsGet(request.Method)
            && !HttpMethods.IsHead(request.Method)
            && request.Headers[ApiHeaders.RequestedWith] != ApiHeaders.RequestedWithValue)
        {
            return TypedResults.Problem(detail: $"The {ApiHeaders.RequestedWith} header is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}
