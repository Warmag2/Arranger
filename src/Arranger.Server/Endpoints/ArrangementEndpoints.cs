using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Data.Queries;
using Faemiyah.Arranger.Grains.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Orleans;

namespace Faemiyah.Arranger.Server.Endpoints;

/// <summary>
/// The arrangement API. Plumbing only: every rule lives in <see cref="IArrangementGrain"/>.
/// </summary>
public static class ArrangementEndpoints
{
    /// <summary>
    /// Map the endpoints under <c>/api/arrangements</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The group.</returns>
    public static RouteGroupBuilder MapArrangementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapApiGroup("/api/arrangements");
        group.MapGet("/", GetMineAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", UpdateDetailsAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/{id:guid}/join", JoinAsync);
        group.MapPost("/{id:guid}/leave", LeaveAsync);
        group.MapPut("/{id:guid}/participation", SetParticipationAsync);
        group.MapPut("/{id:guid}/tabs", SetTabsAsync);
        group.MapPut("/{id:guid}/members/{memberId:guid}/admin", SetAdminAsync);
        group.MapPost("/{id:guid}/time-options", AddTimeOptionAsync);
        group.MapDelete("/{id:guid}/time-options/{optionId:long}", RemoveTimeOptionAsync);
        group.MapPut("/{id:guid}/time-options/{optionId:long}/vote", SetVoteAsync);
        group.MapPut("/{id:guid}/pick", PickTimeAsync);
        group.MapPost("/{id:guid}/expenses", AddExpenseAsync);
        group.MapPut("/{id:guid}/expenses/{expenseId:long}", UpdateExpenseAsync);
        group.MapDelete("/{id:guid}/expenses/{expenseId:long}", DeleteExpenseAsync);
        group.MapPut("/{id:guid}/base-currency", SetBaseCurrencyAsync);
        group.MapPut("/{id:guid}/rates/{currency}", SetExchangeRateAsync);
        group.MapDelete("/{id:guid}/rates/{currency}", RemoveCurrencyAsync);
        return group;
    }

    private static async Task<Ok<List<ArrangementSummary>>> GetMineAsync(ClaimsPrincipal user, ArrangementQueries queries, CancellationToken cancellationToken)
    {
        var rows = await queries.GetForUserAsync(user.GetRequiredUserId(), cancellationToken);
        return TypedResults.Ok(rows.Select(r => new ArrangementSummary(r.UniqueId, r.Title, r.CreatedAt, r.MemberCount, r.IsOwner, r.IsAdmin)).ToList());
    }

    private static async Task<IResult> CreateAsync(CreateArrangementRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        var id = Guid.NewGuid();
        var result = await Grain(grains, id).CreateAsync(user.GetRequiredUserId(), request);
        return result.IsSuccess ? TypedResults.Created($"/api/arrangements/{id}", result.Snapshot) : result.ToHttpResult();
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).GetSnapshotAsync(user.GetRequiredUserId())).ToHttpResult();
    }

    private static async Task<IResult> UpdateDetailsAsync(Guid id, UpdateArrangementDetailsRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).UpdateDetailsAsync(user.GetRequiredUserId(), request)).ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(Guid id, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).DeleteAsync(user.GetRequiredUserId())).ToHttpResult();
    }

    private static async Task<IResult> JoinAsync(Guid id, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).JoinAsync(user.GetRequiredUserId())).ToHttpResult();
    }

    private static async Task<IResult> LeaveAsync(Guid id, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).LeaveAsync(user.GetRequiredUserId())).ToHttpResult();
    }

    private static async Task<IResult> SetParticipationAsync(Guid id, SetParticipationRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).SetParticipationOpenAsync(user.GetRequiredUserId(), request.IsOpen)).ToHttpResult();
    }

    private static async Task<IResult> SetTabsAsync(Guid id, SetTabsRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).SetTabsAsync(user.GetRequiredUserId(), request)).ToHttpResult();
    }

    private static async Task<IResult> SetAdminAsync(Guid id, Guid memberId, SetAdminRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).SetAdminAsync(user.GetRequiredUserId(), memberId, request.IsAdmin)).ToHttpResult();
    }

    private static async Task<IResult> AddTimeOptionAsync(Guid id, AddTimeOptionRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).AddTimeOptionAsync(user.GetRequiredUserId(), request)).ToHttpResult();
    }

    private static async Task<IResult> RemoveTimeOptionAsync(Guid id, long optionId, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).RemoveTimeOptionAsync(user.GetRequiredUserId(), optionId)).ToHttpResult();
    }

    private static async Task<IResult> SetVoteAsync(Guid id, long optionId, SetVoteRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).SetVoteAsync(user.GetRequiredUserId(), optionId, request.Answer)).ToHttpResult();
    }

    private static async Task<IResult> PickTimeAsync(Guid id, PickTimeRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).PickTimeAsync(user.GetRequiredUserId(), request.TimeOptionId)).ToHttpResult();
    }

    private static async Task<IResult> AddExpenseAsync(Guid id, ExpenseRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).AddExpenseAsync(user.GetRequiredUserId(), request)).ToHttpResult();
    }

    private static async Task<IResult> UpdateExpenseAsync(Guid id, long expenseId, ExpenseRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).UpdateExpenseAsync(user.GetRequiredUserId(), expenseId, request)).ToHttpResult();
    }

    private static async Task<IResult> DeleteExpenseAsync(Guid id, long expenseId, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).DeleteExpenseAsync(user.GetRequiredUserId(), expenseId)).ToHttpResult();
    }

    private static async Task<IResult> SetBaseCurrencyAsync(Guid id, SetBaseCurrencyRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).SetBaseCurrencyAsync(user.GetRequiredUserId(), request.Currency)).ToHttpResult();
    }

    private static async Task<IResult> SetExchangeRateAsync(Guid id, string currency, SetExchangeRateRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).SetExchangeRateAsync(user.GetRequiredUserId(), currency, request)).ToHttpResult();
    }

    private static async Task<IResult> RemoveCurrencyAsync(Guid id, string currency, ClaimsPrincipal user, IGrainFactory grains)
    {
        return (await Grain(grains, id).RemoveCurrencyAsync(user.GetRequiredUserId(), currency)).ToHttpResult();
    }

    private static IArrangementGrain Grain(IGrainFactory grains, Guid id)
    {
        return grains.GetGrain<IArrangementGrain>(id);
    }
}
