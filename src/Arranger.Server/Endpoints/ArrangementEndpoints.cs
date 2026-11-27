using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts;
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
    private const string Prefix = "/" + ApiRoutes.Arrangements;

    /// <summary>
    /// Map the endpoints under <c>/api/arrangements</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The group.</returns>
    public static RouteGroupBuilder MapArrangementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapApiGroup(Prefix);
        group.MapGet("/", GetMineAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:guid}", ([AsParameters] ArrangementCall call) =>
            call.RunAsync((grain, _) => grain.GetSnapshotAsync()));
        group.MapPut("/{id:guid}", ([AsParameters] ArrangementCall call, UpdateArrangementDetailsRequest request) =>
            call.RunAsync((grain, user) => grain.UpdateDetailsAsync(user, request)));
        group.MapDelete("/{id:guid}", ([AsParameters] ArrangementCall call) =>
            call.RunAsync((grain, user) => grain.DeleteAsync(user)));
        group.MapPost("/{id:guid}/join", ([AsParameters] ArrangementCall call) =>
            call.RunAsync((grain, user) => grain.JoinAsync(user)));
        group.MapPost("/{id:guid}/leave", ([AsParameters] ArrangementCall call) =>
            call.RunAsync((grain, user) => grain.LeaveAsync(user)));
        group.MapPut("/{id:guid}/participation", ([AsParameters] ArrangementCall call, SetParticipationRequest request) =>
            call.RunAsync((grain, user) => grain.SetParticipationOpenAsync(user, request.IsOpen)));
        group.MapPut("/{id:guid}/new-times", ([AsParameters] ArrangementCall call, SetAllowNewTimesRequest request) =>
            call.RunAsync((grain, user) => grain.SetAllowNewTimesAsync(user, request.IsAllowed)));
        group.MapPut("/{id:guid}/tabs", ([AsParameters] ArrangementCall call, SetTabsRequest request) =>
            call.RunAsync((grain, user) => grain.SetTabsAsync(user, request)));
        group.MapPut("/{id:guid}/members/{memberId:guid}/admin", ([AsParameters] ArrangementCall call, Guid memberId, SetAdminRequest request) =>
            call.RunAsync((grain, user) => grain.SetAdminAsync(user, memberId, request.IsAdmin)));
        group.MapPost("/{id:guid}/time-options", ([AsParameters] ArrangementCall call, AddTimeOptionRequest request) =>
            call.RunAsync((grain, user) => grain.AddTimeOptionAsync(user, request)));
        group.MapDelete("/{id:guid}/time-options/{optionId:long}", ([AsParameters] ArrangementCall call, long optionId) =>
            call.RunAsync((grain, user) => grain.RemoveTimeOptionAsync(user, optionId)));
        group.MapPut("/{id:guid}/time-options/{optionId:long}/vote", ([AsParameters] ArrangementCall call, long optionId, SetVoteRequest request) =>
            call.RunAsync((grain, user) => grain.SetVoteAsync(user, optionId, request.Answer)));
        group.MapPut("/{id:guid}/pick", ([AsParameters] ArrangementCall call, PickTimeRequest request) =>
            call.RunAsync((grain, user) => grain.PickTimeAsync(user, request.TimeOptionId)));
        group.MapPost("/{id:guid}/expenses", ([AsParameters] ArrangementCall call, ExpenseRequest request) =>
            call.RunAsync((grain, user) => grain.AddExpenseAsync(user, request)));
        group.MapPut("/{id:guid}/expenses/{expenseId:long}", ([AsParameters] ArrangementCall call, long expenseId, ExpenseRequest request) =>
            call.RunAsync((grain, user) => grain.UpdateExpenseAsync(user, expenseId, request)));
        group.MapDelete("/{id:guid}/expenses/{expenseId:long}", ([AsParameters] ArrangementCall call, long expenseId) =>
            call.RunAsync((grain, user) => grain.DeleteExpenseAsync(user, expenseId)));
        group.MapPut("/{id:guid}/base-currency", ([AsParameters] ArrangementCall call, SetBaseCurrencyRequest request) =>
            call.RunAsync((grain, user) => grain.SetBaseCurrencyAsync(user, request.Currency)));
        group.MapPut("/{id:guid}/rates/{currency}", ([AsParameters] ArrangementCall call, string currency, SetExchangeRateRequest request) =>
            call.RunAsync((grain, user) => grain.SetExchangeRateAsync(user, currency, request)));
        group.MapDelete("/{id:guid}/rates/{currency}", ([AsParameters] ArrangementCall call, string currency) =>
            call.RunAsync((grain, user) => grain.RemoveCurrencyAsync(user, currency)));
        group.MapPost("/{id:guid}/rates/refresh", ([AsParameters] ArrangementCall call) =>
            call.RunAsync((grain, user) => grain.RefreshRatesAsync(user)));
        return group;
    }

    private static async Task<Ok<IReadOnlyList<ArrangementSummary>>> GetMineAsync(ClaimsPrincipal user, ArrangementQueries queries, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await queries.GetForUserAsync(user.GetRequiredUserId(), cancellationToken));
    }

    private static async Task<IResult> CreateAsync(CreateArrangementRequest request, ClaimsPrincipal user, IGrainFactory grains)
    {
        var id = Guid.NewGuid();
        var result = await grains.GetGrain<IArrangementGrain>(id).CreateAsync(user.GetRequiredUserId(), request);
        return result.IsSuccess ? TypedResults.Created($"{Prefix}/{id}", result.Snapshot) : result.ToHttpResult();
    }

    /// <summary>
    /// The inputs every arrangement command shares: the arrangement from the route, the signed-in user, and the grains.
    /// </summary>
    /// <param name="Id">The public ID of the arrangement.</param>
    /// <param name="User">The signed-in user.</param>
    /// <param name="Grains">The grain factory.</param>
    private sealed record ArrangementCall(Guid Id, ClaimsPrincipal User, IGrainFactory Grains)
    {
        /// <summary>
        /// Run a command on the arrangement's grain as the signed-in user.
        /// </summary>
        /// <param name="command">The command, given the grain and the user's public ID.</param>
        /// <returns>The command's HTTP result.</returns>
        public async Task<IResult> RunAsync(Func<IArrangementGrain, Guid, Task<CommandResult>> command)
        {
            return (await command(Grains.GetGrain<IArrangementGrain>(Id), User.GetRequiredUserId())).ToHttpResult();
        }
    }
}
