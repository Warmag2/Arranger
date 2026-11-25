using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts;
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
        var group = endpoints.MapApiGroup("/");
        group.MapGet("/" + ApiRoutes.MySettings, GetSettingsAsync);
        group.MapPut("/" + ApiRoutes.MySettings, UpdateSettingsAsync);
        group.MapPut("/" + ApiRoutes.MyPicture, SetPictureAsync);
        group.MapDelete("/" + ApiRoutes.MyPicture, DeletePictureAsync);
        group.MapGet("/" + ApiRoutes.Users + "/{userId:guid}/picture", GetPictureAsync);
        group.MapGet("/" + ApiRoutes.TimeZones, (TimeZoneCatalog catalog) => TypedResults.Ok(catalog.GetTimeZones()));
        return group;
    }

    private static async Task<IResult> GetSettingsAsync(ClaimsPrincipal principal, IEntityRepositoryWithUuid<long, User> users, UserQueries queries, CancellationToken cancellationToken)
    {
        var userId = principal.GetRequiredUserId();
        var user = await users.GetAsync(userId, cancellationToken: cancellationToken);
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
        var user = await users.GetAsync(userId, cancellationToken: cancellationToken);
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
                OperationStatus.Duplicate => TypedResults.Problem("The username is taken.", statusCode: StatusCodes.Status409Conflict),
                _ => TypedResults.Problem("The settings were changed elsewhere. Reload and try again.", statusCode: StatusCodes.Status409Conflict),
            };
        }

        if (isRenamed)
        {
            // The username is a cookie claim, so reissue the cookie, and show the new username in every arrangement.
            var authentication = await context.AuthenticateAsync();
            await context.SignInAsync(AccountService.CreatePrincipal(updated.Value), authentication.Properties);
            await RefreshArrangementsAsync(userId, arrangements, grains, cancellationToken);
        }

        return TypedResults.Ok(await ToSettingsAsync(updated.Value, queries, cancellationToken));
    }

    private static async Task<IResult> SetPictureAsync(
        HttpRequest request,
        IEntityRepositoryWithUuid<long, User> users,
        IEntityRepository<long, UserPicture> pictures,
        UserQueries queries,
        ArrangementQueries arrangements,
        IGrainFactory grains,
        CancellationToken cancellationToken)
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

        // Read the dimensions from the header without decoding the pixels, so a tiny file cannot expand to a huge
        // bitmap in the browsers that show it.
        var dimensions = ProfilePictureRules.ReadDimensions(data);
        if (dimensions is not { } size || size.Width > ProfilePictureRules.MaxDimension || size.Height > ProfilePictureRules.MaxDimension)
        {
            return TypedResults.Problem(
                $"The picture can be at most {ProfilePictureRules.MaxDimension}×{ProfilePictureRules.MaxDimension} pixels.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var userId = request.HttpContext.User.GetRequiredUserId();
        var user = await users.GetAsync(userId, cancellationToken: cancellationToken);
        if (user == null)
        {
            return TypedResults.NotFound();
        }

        var picture = new UserPicture { UserId = user.Id, Data = data, ContentType = contentType };
        var result = await pictures.AddOrUpdateAsync(picture, p => p.UserId == user.Id, cancellationToken);
        return result.IsSuccess ? await PictureChangedAsync(user, queries, arrangements, grains, cancellationToken) : Conflict();
    }

    private static async Task<IResult> DeletePictureAsync(
        ClaimsPrincipal principal,
        IEntityRepositoryWithUuid<long, User> users,
        IEntityRepository<long, UserPicture> pictures,
        UserQueries queries,
        ArrangementQueries arrangements,
        IGrainFactory grains,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetRequiredUserId();
        var user = await users.GetAsync(userId, cancellationToken: cancellationToken);
        if (user == null)
        {
            return TypedResults.NotFound();
        }

        var result = await pictures.DeleteAsync(p => p.UserId == user.Id, cancellationToken);
        return result.IsSuccess ? await PictureChangedAsync(user, queries, arrangements, grains, cancellationToken) : Conflict();
    }

    private static async Task<IResult> GetPictureAsync(Guid userId, HttpContext context, UserQueries queries, CancellationToken cancellationToken)
    {
        var picture = await queries.GetPictureAsync(userId, cancellationToken);
        if (picture == null)
        {
            return TypedResults.NotFound();
        }

        // The client asks with the picture's version in the URL, so a cached copy never goes stale.
        var headers = context.Response.Headers;
        headers.CacheControl = "private, max-age=31536000, immutable";
        headers.XContentTypeOptions = "nosniff";
        return TypedResults.Bytes(picture.Data, picture.ContentType);
    }

    private static async Task<IResult> PictureChangedAsync(User user, UserQueries queries, ArrangementQueries arrangements, IGrainFactory grains, CancellationToken cancellationToken)
    {
        // Show the new picture version in every arrangement, and to the user.
        await RefreshArrangementsAsync(user.UniqueId, arrangements, grains, cancellationToken);
        return TypedResults.Ok(await ToSettingsAsync(user, queries, cancellationToken));
    }

    private static async Task RefreshArrangementsAsync(Guid userId, ArrangementQueries arrangements, IGrainFactory grains, CancellationToken cancellationToken)
    {
        var rows = await arrangements.GetForUserAsync(userId, cancellationToken);
        await Task.WhenAll(rows.Select(r => grains.GetGrain<IArrangementGrain>(r.Id).RefreshMemberAsync(userId)));
    }

    private static async Task<UserSettings> ToSettingsAsync(User user, UserQueries queries, CancellationToken cancellationToken)
    {
        var profile = await queries.GetProfileAsync(user.UniqueId, cancellationToken);
        return new UserSettings(user.UniqueId, user.UserName, user.TimeZoneId, user.DefaultCurrency, profile?.PictureVersion);
    }

    private static ProblemHttpResult Conflict()
    {
        return TypedResults.Problem("The picture was changed elsewhere. Try again.", statusCode: StatusCodes.Status409Conflict);
    }
}
