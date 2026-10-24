using System;
using System.Linq;
using System.Security;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Accounts;
using Faemiyah.Arranger.Contracts.Users;
using Faemiyah.Arranger.Data.Entities;
using Faemiyah.Arranger.Data.Queries;
using Faemiyah.Arranger.Grains.Abstractions;
using Faemiyah.Arranger.Server.Authentication;
using Faemiyah.Arranger.Server.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Orleans;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Models;

namespace Faemiyah.Arranger.Server.Endpoints;

/// <summary>
/// The user API: the signed-in user's settings and picture, everyone's pictures, and the time zone list.
/// </summary>
public static class UserEndpoints
{
    /// <summary>
    /// Map the endpoints under <c>/api</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The group.</returns>
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapApiGroup("/api");
        group.MapGet("/me/settings", GetSettingsAsync);
        group.MapPut("/me/settings", UpdateSettingsAsync);
        group.MapPut("/me/picture", SetPictureAsync);
        group.MapDelete("/me/picture", DeletePictureAsync);
        group.MapGet("/users/{userId:guid}/picture", GetPictureAsync);
        group.MapGet("/time-zones", (TimeZoneCatalog catalog) => TypedResults.Ok(catalog.GetTimeZones()));
        return group;
    }

    private static async Task<IResult> GetSettingsAsync(ClaimsPrincipal principal, IEntityRepositoryWithUuid<long, User> users, UserQueries queries, CancellationToken cancellationToken)
    {
        var userId = principal.GetRequiredUserId();
        var user = await users.GetAsync(u => u.UniqueId == userId, cancellationToken: cancellationToken);
        return user == null ? TypedResults.NotFound() : TypedResults.Ok(await ToSettingsAsync(user, queries, cancellationToken));
    }

    private static async Task<IResult> UpdateSettingsAsync(
        UpdateUserSettingsRequest request,
        HttpContext context,
        IEntityRepositoryWithUuid<long, User> users,
        UserQueries queries,
        ArrangementQueries arrangements,
        IGrainFactory grains,
        CancellationToken cancellationToken)
    {
        var error = AccountRules.ValidateSettings(request.UserName, request.TimeZoneId, request.DefaultCurrency);
        if (error != null)
        {
            return TypedResults.Problem(error, statusCode: StatusCodes.Status400BadRequest);
        }

        var userId = context.User.GetRequiredUserId();
        var user = await users.GetAsync(u => u.UniqueId == userId, cancellationToken: cancellationToken);
        if (user == null)
        {
            return TypedResults.NotFound();
        }

        var isRenamed = user.UserName != request.UserName;
        user.UserName = request.UserName;
        user.TimeZoneId = request.TimeZoneId;
        user.DefaultCurrency = request.DefaultCurrency;

        var updated = await users.UpdateAsync(user, cancellationToken);
        if (!updated.IsSuccess)
        {
            return updated.Status switch
            {
                OperationStatus.NotFound => TypedResults.NotFound(),
                OperationStatus.Duplicate => TypedResults.Problem("The handle is taken.", statusCode: StatusCodes.Status409Conflict),
                _ => TypedResults.Problem("The settings were changed elsewhere. Reload and try again.", statusCode: StatusCodes.Status409Conflict),
            };
        }

        if (isRenamed)
        {
            // The handle is a cookie claim, so reissue the cookie, and show the new handle in every arrangement.
            var authentication = await context.AuthenticateAsync();
            await context.SignInAsync(AccountService.CreatePrincipal(updated.Value), authentication.Properties);
            var rows = await arrangements.GetForUserAsync(userId, cancellationToken);
            await Task.WhenAll(rows.Select(r => grains.GetGrain<IArrangementGrain>(r.UniqueId).RefreshMemberAsync(userId)));
        }

        return TypedResults.Ok(await ToSettingsAsync(updated.Value, queries, cancellationToken));
    }

    private static async Task<IResult> SetPictureAsync(HttpRequest request, IEntityRepository<long, UserPicture> pictures, UserQueries queries, CancellationToken cancellationToken)
    {
        // One byte over the limit tells an oversized upload apart without reading all of it.
        var buffer = new byte[ProfilePictureRules.MaxBytes + 1];
        var length = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        if (length > ProfilePictureRules.MaxBytes)
        {
            return TypedResults.Problem("The picture is too large.", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var data = buffer[..length];
        var contentType = ProfilePictureRules.DetectContentType(data);
        if (contentType == null)
        {
            return TypedResults.Problem("The picture must be PNG, JPEG or WebP.", statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        var profile = await queries.GetProfileAsync(request.HttpContext.User.GetRequiredUserId(), cancellationToken);
        if (profile == null)
        {
            return TypedResults.NotFound();
        }

        var picture = new UserPicture { UserId = profile.Id, Data = data, ContentType = contentType };
        var result = await pictures.AddOrUpdateAsync(picture, p => p.UserId == profile.Id, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : Conflict();
    }

    private static async Task<IResult> DeletePictureAsync(ClaimsPrincipal principal, IEntityRepository<long, UserPicture> pictures, UserQueries queries, CancellationToken cancellationToken)
    {
        var profile = await queries.GetProfileAsync(principal.GetRequiredUserId(), cancellationToken);
        if (profile == null)
        {
            return TypedResults.NotFound();
        }

        var result = await pictures.DeleteAsync(p => p.UserId == profile.Id, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : Conflict();
    }

    private static async Task<IResult> GetPictureAsync(Guid userId, HttpContext context, UserQueries queries, CancellationToken cancellationToken)
    {
        var picture = await queries.GetPictureAsync(userId, cancellationToken);
        if (picture == null)
        {
            return TypedResults.NotFound();
        }

        // Revalidated on every use, so a changed picture shows at once while an unchanged one is not downloaded.
        var headers = context.Response.Headers;
        headers.CacheControl = "private, no-cache";
        headers.XContentTypeOptions = "nosniff";
        if (picture.Data == null)
        {
            return TypedResults.Text(CreateInitialSvg(userId, picture.UserName), "image/svg+xml");
        }

        var etag = new EntityTagHeaderValue($"\"{picture.Version}\"");
        headers.ETag = etag.ToString();
        var requested = context.Request.GetTypedHeaders().IfNoneMatch;
        return requested.Any(t => t.Compare(etag, useStrongComparison: false))
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Bytes(picture.Data, picture.ContentType);
    }

    private static async Task<UserSettings> ToSettingsAsync(User user, UserQueries queries, CancellationToken cancellationToken)
    {
        var hasPicture = await queries.HasPictureAsync(user.UniqueId, cancellationToken);
        return new UserSettings(user.UniqueId, user.UserName, user.TimeZoneId, user.DefaultCurrency, hasPicture);
    }

    private static string CreateInitialSvg(Guid userId, string userName)
    {
        var hue = userId.ToByteArray()[0] * 360 / 256;
        var initial = SecurityElement.Escape(userName[..1].ToUpperInvariant());
        return $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect width="64" height="64" fill="hsl({hue},45%,45%)"/><text x="32" y="32" dy=".35em" text-anchor="middle" font-family="sans-serif" font-size="32" fill="#fff">{initial}</text></svg>""";
    }

    private static ProblemHttpResult Conflict()
    {
        return TypedResults.Problem("The picture was changed elsewhere. Try again.", statusCode: StatusCodes.Status409Conflict);
    }
}
