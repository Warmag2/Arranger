using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Data.Entities;
using Faemiyah.Arranger.Data.Extensions;
using Faemiyah.Arranger.Grains;
using Faemiyah.Arranger.Grains.Abstractions;
using Faemiyah.Arranger.Server.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Orleans;
using Orleans.TestingHost;
using Faemiyah.Repository.Interfaces;

namespace Faemiyah.Arranger.IntegrationTests;

[TestFixture]
public class ArrangementGrainTests
{
    private static readonly DateTime Evening = new(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);

    private static readonly TestCaseData[] ExpenseCommands =
    [
        ExpenseCommandCase("AddExpense", (grain, owner, _) => grain.AddExpenseAsync(owner, Dinner(owner, owner))),
        ExpenseCommandCase("UpdateExpense", (grain, owner, id) => grain.UpdateExpenseAsync(owner, id, Dinner(owner, owner))),
        ExpenseCommandCase("DeleteExpense", (grain, owner, id) => grain.DeleteExpenseAsync(owner, id)),
        ExpenseCommandCase("SetExchangeRate", (grain, owner, _) => grain.SetExchangeRateAsync(owner, "EUR", new(0.09m))),
    ];

    private static readonly TestCaseData[] InvalidExchangeRates =
    [
        new TestCaseData("SEK", new SetExchangeRateRequest(1)).SetArgDisplayNames("BaseCurrency"),
        new TestCaseData("EUR", new SetExchangeRateRequest(0)).SetArgDisplayNames("ZeroRate"),
        new TestCaseData("EUR", new SetExchangeRateRequest(11.5m, 50.01m)).SetArgDisplayNames("LossOverHalf"),
    ];

    private static readonly TestCaseData[] InvalidExpenses =
    [
        InvalidExpenseCase("OutsiderParticipant", (owner, outsider) => Dinner(owner, owner, outsider)),
        InvalidExpenseCase("NoParticipants", (owner, _) => Dinner(owner)),
        InvalidExpenseCase("ZeroAmount", (owner, _) => Dinner(owner, owner) with { Amount = 0 }),
        InvalidExpenseCase("CurrencyNotAdded", (owner, _) => Dinner(owner, owner) with { Currency = "JPY" }),
        InvalidExpenseCase("SettlementToPayer", (owner, _) => Dinner(owner, owner) with { IsSettlementPayment = true }),
    ];

    private static readonly TestCaseData[] MissingArrangementCommands =
    [
        MissingArrangementCase("GetSnapshot", (grain, _) => grain.GetSnapshotAsync()),
        MissingArrangementCase("Join", (grain, user) => grain.JoinAsync(user)),
        MissingArrangementCase("SetParticipationOpen", (grain, user) => grain.SetParticipationOpenAsync(user, false)),
        MissingArrangementCase("Delete", (grain, user) => grain.DeleteAsync(user)),
    ];

    private static readonly CreateArrangementRequest Request = new("Sauna evening", "Bring towels.");

    private static readonly TestCaseData[] ScheduleCommands =
    [
        ScheduleCommandCase("SetVote", (grain, _, member, option) => grain.SetVoteAsync(member, option, VoteAnswer.Yes)),
        ScheduleCommandCase("AddTimeOption", (grain, _, member, _) => grain.AddTimeOptionAsync(member, Timed(20))),
        ScheduleCommandCase("RemoveTimeOption", (grain, owner, _, option) => grain.RemoveTimeOptionAsync(owner, option)),
        ScheduleCommandCase("PickTime", (grain, owner, _, option) => grain.PickTimeAsync(owner, option)),
    ];

    private readonly RecordingNotifier _notifier = new();
    private readonly FakeCurrencyRateProvider _rateProvider = new();
    private IEntityRepositoryWithUuid<long, Arrangement> _arrangements = null!;
    private InProcessTestCluster _cluster = null!;
    private IEntityRepository<long, UserPicture> _pictures = null!;
    private IEntityRepositoryWithUuid<long, User> _users = null!;

    [OneTimeSetUp]
    public async Task StartClusterAsync()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = PostgreSqlFixture.BaseConnectionString,
            ["Sql:DefaultDbName"] = "arranger_" + Guid.NewGuid().ToString("N"),
        };

        var builder = new InProcessTestClusterBuilder(1);
        builder.ConfigureHost(host => host.Services.AddArrangerSerializer());
        builder.ConfigureSiloHost((_, host) =>
        {
            host.Configuration.AddInMemoryCollection(settings);
            host.Services.AddArrangerData(host.Configuration);
            host.Services.AddSingleton<IArrangementNotifier>(_notifier);
            host.Services.AddSingleton<ICurrencyRateProvider>(_rateProvider);
        });

        _cluster = builder.Build();
        await _cluster.DeployAsync();
        var services = _cluster.GetSiloServiceProvider(_cluster.Silos[0].SiloAddress);
        _users = services.GetRequiredService<IEntityRepositoryWithUuid<long, User>>();
        _arrangements = services.GetRequiredService<IEntityRepositoryWithUuid<long, Arrangement>>();
        _pictures = services.GetRequiredService<IEntityRepository<long, UserPicture>>();
    }

    [OneTimeTearDown]
    public async Task StopClusterAsync()
    {
        await _cluster.DisposeAsync();
    }

    [SetUp]
    public void ResetRateProvider()
    {
        _rateProvider.Rates.Clear();
    }

    [Test]
    public async Task AddExpense_ByMember_AddsExpense()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        var third = await AddUserAsync("Third");
        await grain.JoinAsync(third);

        var result = await grain.AddExpenseAsync(member, Dinner(owner, owner, member, third));

        var added = result.Snapshot!.Expenses.Single();
        added.Description.Should().Be("Dinner");
        added.PaidByUserId.Should().Be(owner);
        added.ParticipantUserIds.Should().BeEquivalentTo([owner, member, third]);
    }

    [Test]
    public async Task AddExpense_ByOutsider_ReturnsNotMember()
    {
        var owner = await AddUserAsync("Owner");
        var outsider = await AddUserAsync("Outsider");
        var grain = await CreateAsync(owner);

        var result = await grain.AddExpenseAsync(outsider, Dinner(outsider, outsider));

        result.Error.Should().Be(CommandError.NotMember);
    }

    [Test]
    public async Task AddExpense_SettlementPayment_SettlesBalances()
    {
        var (grain, _, member) = await CreateWithMemberAsync();
        var third = await AddUserAsync("Third");
        await grain.JoinAsync(third);
        var dinner = (await grain.AddExpenseAsync(member, Dinner(member, member, third))).Snapshot!.Expenses.Single();
        var payment = ExpenseMath.SuggestPayments([dinner], dinner.Date).Single();

        var result = await grain.AddExpenseAsync(third, payment);

        var expenses = result.Snapshot!.Expenses;
        ExpenseMath.SuggestPayments(expenses, dinner.Date).Should().BeEmpty();
        expenses.Select(e => e.IsSettlementPayment).Should().Equal(false, true);
    }

    [TestCaseSource(nameof(InvalidExpenses))]
    public async Task AddExpense_WithInvalidRequest_ReturnsInvalid(Func<Guid, Guid, ExpenseRequest> request)
    {
        var owner = await AddUserAsync("Owner");
        var outsider = await AddUserAsync("Outsider");
        var grain = await CreateAsync(owner);

        var result = await grain.AddExpenseAsync(owner, request(owner, outsider));

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task AddExpense_WithMoreDecimalsThanTheCurrency_ReturnsInvalid()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "JPY", new(null));

        var result = await grain.AddExpenseAsync(owner, Dinner(owner, owner) with { Currency = "JPY", Amount = 1.5m });

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task AddTimeOption_ByAdminWhileNewTimesClosed_AddsTime()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.AddTimeOptionAsync(owner, Timed(18));

        result.Snapshot!.TimeOptions.Should().HaveCount(1);
    }

    [Test]
    public async Task AddTimeOption_ByMemberWhileNewTimesClosed_ReturnsForbidden()
    {
        // Closed by default: only admins can add times.
        var (grain, _, member) = await CreateWithMemberAsync();

        var result = await grain.AddTimeOptionAsync(member, Timed(18));

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task AddTimeOption_ByMemberWhileNewTimesOpen_AddsTime()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetAllowNewTimesAsync(owner, true);

        var result = await grain.AddTimeOptionAsync(member, Timed(20));

        result.Snapshot!.TimeOptions.Should().HaveCount(1);
    }

    [Test]
    public async Task AddTimeOption_ByOutsider_ReturnsNotMember()
    {
        var owner = await AddUserAsync("Owner");
        var outsider = await AddUserAsync("Outsider");
        var grain = await CreateAsync(owner);

        var result = await grain.AddTimeOptionAsync(outsider, Timed(20));

        result.Error.Should().Be(CommandError.NotMember);
    }

    [Test]
    public async Task AddTimeOption_WholeDayAtMidnight_AddsTimeBesideTimedOnes()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await AddOptionAsync(grain, owner, 18);

        var result = await grain.AddTimeOptionAsync(owner, new AddTimeOptionRequest(Evening, null, true));

        result.Snapshot!.TimeOptions.Should().HaveCount(2);
    }

    [Test]
    public async Task AddTimeOption_WholeDayNotAtMidnight_ReturnsInvalid()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.AddTimeOptionAsync(owner, new AddTimeOptionRequest(Evening.AddHours(1), null, true));

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task AddTimeOption_WithSeconds_AddsTimeAtTheMinute()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.AddTimeOptionAsync(owner, Timed(18) with { DateBegin = Evening.AddHours(18).AddSeconds(30) });

        result.Snapshot!.TimeOptions.Should().ContainSingle().Which.DateBegin.Should().Be(Evening.AddHours(18));
    }

    [Test]
    public async Task AddTimeOption_WithTakenTime_ReturnsInvalid()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.AddTimeOptionAsync(owner, Timed(18) with { DateBegin = Evening.AddHours(18).AddSeconds(30) });

        var result = await grain.AddTimeOptionAsync(owner, Timed(18));

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task Changes_AreNotifiedInOrder_NoOpsAndRefusalsAreNot()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var grain = await CreateAsync(owner);

        await grain.JoinAsync(member);
        await grain.JoinAsync(member);
        await grain.SetParticipationOpenAsync(member, false);
        await grain.GetSnapshotAsync();
        await grain.DeleteAsync(owner);

        var notifications = _notifier.For(grain.GetPrimaryKey());
        notifications.Should().HaveCount(3);
        notifications[0]!.Members.Should().HaveCount(1);
        notifications[1]!.Members.Should().HaveCount(2);
        notifications[1]!.Version.Supersedes(notifications[0]!.Version).Should().BeTrue();
        notifications[2].Should().BeNull();
    }

    [TestCaseSource(nameof(MissingArrangementCommands))]
    public async Task Command_OnMissingArrangement_ReturnsNotFound(Func<IArrangementGrain, Guid, Task<CommandResult>> command)
    {
        var user = await AddUserAsync("Someone");
        var grain = NewGrain();

        var result = await command(grain, user);

        result.Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task Create_ByUnknownUser_ReturnsNotFound()
    {
        var grain = NewGrain();

        var result = await grain.CreateAsync(Guid.NewGuid(), Request);

        result.Error.Should().Be(CommandError.NotFound);
        (await grain.GetSnapshotAsync()).Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task Create_ClosesNewTimesToMembers()
    {
        var owner = await AddUserAsync("Owner");
        var grain = NewGrain();

        var result = await grain.CreateAsync(owner, Request);

        result.Snapshot!.AllowNewTimes.Should().BeFalse();
    }

    [Test]
    public async Task Create_MakesCreatorOwnerAndAdmin()
    {
        var owner = await AddUserAsync("Owner");
        var grain = NewGrain();

        var result = await grain.CreateAsync(owner, Request with { Title = "  Sauna evening  " });

        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Snapshot!;
        snapshot.Title.Should().Be("Sauna evening");
        snapshot.OwnerUserId.Should().Be(owner);
        snapshot.BaseCurrency.Should().Be("SEK");
        snapshot.Members.Should().ContainSingle().Which.Should().Match<ArrangementMemberSnapshot>(m => m.UserId == owner && m.IsAdmin && m.PictureVersion == null);
    }

    [Test]
    public async Task Create_WhenAlreadyCreated_ReturnsConflict()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.CreateAsync(owner, Request);

        result.Error.Should().Be(CommandError.Conflict);
    }

    [Test]
    public async Task Create_WithBlankTitle_ReturnsInvalid()
    {
        var owner = await AddUserAsync("Owner");
        var grain = NewGrain();

        var result = await grain.CreateAsync(owner, Request with { Title = " " });

        result.Error.Should().Be(CommandError.Invalid);
        (await grain.GetSnapshotAsync()).Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task Create_WithExpensesDisabled_DisablesExpenses()
    {
        var owner = await AddUserAsync("Owner");
        var grain = NewGrain();

        var result = await grain.CreateAsync(owner, Request with { IsExpensesDisabled = true });

        result.Snapshot!.IsExpensesDisabled.Should().BeTrue();
    }

    [Test]
    public async Task Delete_ByAdminNotOwner_ReturnsForbidden()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetAdminAsync(owner, member, true);

        var result = await grain.DeleteAsync(member);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task Delete_RemovesArrangement()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.DeleteAsync(owner);

        result.IsSuccess.Should().BeTrue();
        result.Snapshot.Should().BeNull();
        (await grain.GetSnapshotAsync()).Error.Should().Be(CommandError.NotFound);
        (await _arrangements.GetAsync(grain.GetPrimaryKey())).Should().BeNull();
    }

    [Test]
    public async Task DeleteExpense_ByAdminNotPayer_DeletesExpense()
    {
        var (grain, owner, payer) = await CreateWithMemberAsync();
        var id = await AddExpenseAsync(grain, payer, Dinner(payer, payer, owner));

        var result = await grain.DeleteExpenseAsync(owner, id);

        result.Snapshot!.Expenses.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteExpense_ByNonPayerNonAdmin_ReturnsForbidden()
    {
        var (grain, _, payer) = await CreateWithMemberAsync();
        var other = await AddUserAsync("Other");
        await grain.JoinAsync(other);
        var id = await AddExpenseAsync(grain, payer, Dinner(payer, payer, other));

        var result = await grain.DeleteExpenseAsync(other, id);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task DeleteExpense_RemovesOnlyThatExpense()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        var third = await AddUserAsync("Third");
        await grain.JoinAsync(third);
        var dinner = (await grain.AddExpenseAsync(member, Dinner(member, member, third))).Snapshot!.Expenses.Single();
        await grain.AddExpenseAsync(third, ExpenseMath.SuggestPayments([dinner], dinner.Date).Single());

        var result = await grain.DeleteExpenseAsync(owner, dinner.Id);

        result.Snapshot!.Expenses.Should().ContainSingle();
        await _cluster.DeactivateAsync(grain);
        (await grain.GetSnapshotAsync()).Snapshot!.Expenses.Should().ContainSingle().Which.IsSettlementPayment.Should().BeTrue();
    }

    [Test]
    public async Task DeleteExpense_WhenAlreadyDeleted_ReturnsNotFound()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var id = await AddExpenseAsync(grain, owner, Dinner(owner, owner));
        await grain.DeleteExpenseAsync(owner, id);

        var result = await grain.DeleteExpenseAsync(owner, id);

        result.Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task DeleteOfMissingRow_ReturnsConflictAndReloads()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var optionId = await AddOptionAsync(grain, owner, 18);
        (await _arrangements.DeleteAsync(grain.GetPrimaryKey())).IsSuccess.Should().BeTrue();

        var stale = await grain.RemoveTimeOptionAsync(owner, optionId);

        stale.Error.Should().Be(CommandError.Conflict);
        await _cluster.WaitForDeactivationAsync(grain);
    }

    [Test]
    public async Task ExchangeRates_SurviveReactivation()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "EUR", new(11.5m));
        await grain.SetExchangeRateAsync(owner, "EUR", new(11.25m, 2.5m));
        await grain.SetExchangeRateAsync(owner, "USD", new(10));

        await _cluster.DeactivateAsync(grain);
        var after = (await grain.GetSnapshotAsync()).Snapshot!;

        after.ExchangeRates.Should().BeEquivalentTo(
            new Dictionary<string, ExchangeRateSnapshot> { ["EUR"] = new(11.25m, 2.5m), ["USD"] = new(10, 0) });
    }

    [TestCaseSource(nameof(ExpenseCommands))]
    public async Task ExpenseCommand_WhileExpensesDisabled_ReturnsForbidden(Func<IArrangementGrain, Guid, long, Task<CommandResult>> command)
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var id = await AddExpenseAsync(grain, owner, Dinner(owner, owner));
        await grain.SetTabsAsync(owner, new SetTabsRequest(false, true));

        var result = await command(grain, owner, id);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task Expenses_SurviveReactivation()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        var third = await AddUserAsync("Third");
        await grain.JoinAsync(third);
        var id = await AddExpenseAsync(grain, member, Dinner(owner, owner, member, third));
        var replaced = (await grain.UpdateExpenseAsync(owner, id, Dinner(member, member, third) with { Amount = 31.5m })).Snapshot!.Expenses.Single();
        var before = (await grain.AddExpenseAsync(third, ExpenseMath.SuggestPayments([replaced], replaced.Date).Single())).Snapshot!;

        await _cluster.DeactivateAsync(grain);
        var after = (await grain.GetSnapshotAsync()).Snapshot!;

        after.Should().BeEquivalentTo(before, o => o.Excluding(s => s.Version).WithStrictOrdering());
    }

    [Test]
    public async Task Join_AddsMember()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var grain = await CreateAsync(owner);

        var result = await grain.JoinAsync(member);

        result.Snapshot!.Members.Should().HaveCount(2);
    }

    [Test]
    public async Task Join_AfterLeaving_AddsMemberAgain()
    {
        var (grain, _, member) = await CreateWithMemberAsync();
        await grain.LeaveAsync(member);

        var result = await grain.JoinAsync(member);

        result.Snapshot!.FindMember(member).Should().NotBeNull();
    }

    [Test]
    public async Task Join_AfterParticipationClosed_ReturnsForbidden()
    {
        var owner = await AddUserAsync("Owner");
        var late = await AddUserAsync("Late");
        var grain = await CreateAsync(owner);
        await grain.SetParticipationOpenAsync(owner, false);

        var result = await grain.JoinAsync(late);

        result.Error.Should().Be(CommandError.Forbidden);
        (await grain.GetSnapshotAsync()).Snapshot!.FindMember(late).Should().BeNull();
    }

    [Test]
    public async Task Join_WhenAlreadyMember_ChangesNothing()
    {
        var (grain, _, member) = await CreateWithMemberAsync();

        var result = await grain.JoinAsync(member);

        result.Snapshot!.Members.Should().HaveCount(2);
    }

    [Test]
    public async Task Leave_ByMember_RemovesMemberAndVotes()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetAllowNewTimesAsync(owner, true);
        var option = await AddOptionAsync(grain, member, 18);
        await grain.SetVoteAsync(member, option, VoteAnswer.Yes);

        var result = await grain.LeaveAsync(member);

        var left = result.Snapshot!;
        left.FindMember(member).Should().BeNull();
        left.TimeOptions.Single().Votes.Should().BeEmpty();
        await _cluster.DeactivateAsync(grain);
        var reloaded = (await grain.GetSnapshotAsync()).Snapshot!;
        reloaded.FindMember(member).Should().BeNull();
        reloaded.TimeOptions.Single().Votes.Should().BeEmpty();
    }

    [Test]
    public async Task Leave_ByMemberInAnExpense_ReturnsInvalid()
    {
        var (grain, owner, payer) = await CreateWithMemberAsync();
        await grain.AddExpenseAsync(payer, Dinner(payer, payer, owner));

        var result = await grain.LeaveAsync(payer);

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task Leave_ByOwner_ReturnsInvalid()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.LeaveAsync(owner);

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task Leave_WhenNotMember_ReturnsNotMember()
    {
        var (grain, _, member) = await CreateWithMemberAsync();
        await grain.LeaveAsync(member);

        var result = await grain.LeaveAsync(member);

        result.Error.Should().Be(CommandError.NotMember);
    }

    [Test]
    public async Task PickTime_ByAdmin_PicksTime()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);

        var result = await grain.PickTimeAsync(owner, option);

        result.Snapshot!.FinalTimeOptionId.Should().Be(option);
        result.Snapshot.IsScheduleDisabled.Should().BeFalse();
    }

    [Test]
    public async Task PickTime_ByNonAdmin_ReturnsForbidden()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        var option = await AddOptionAsync(grain, owner, 18);

        var result = await grain.PickTimeAsync(member, option);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task PickTime_ClearingWhileScheduleDisabled_ReturnsForbidden()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.PickTimeAsync(owner, option);
        await grain.SetTabsAsync(owner, new SetTabsRequest(true, false));

        var result = await grain.PickTimeAsync(owner, null);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task PickTime_OfMissingOption_ReturnsNotFound()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);

        var result = await grain.PickTimeAsync(owner, option + 1000);

        result.Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task PickTime_WithNull_ClearsPick()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.PickTimeAsync(owner, option);

        var result = await grain.PickTimeAsync(owner, null);

        result.Snapshot!.FinalTimeOptionId.Should().BeNull();
    }

    [Test]
    public async Task RefreshMember_AfterPictureAdded_SetsPictureVersion()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var user = (await _users.GetAsync(owner))!;
        var added = await _pictures.AddAsync(new UserPicture { UserId = user.Id, Data = [1], ContentType = "image/png" });

        await grain.RefreshMemberAsync(owner);
        var version = (await grain.GetSnapshotAsync()).Snapshot!.FindMember(owner)!.PictureVersion;

        version.Should().Be(added.Value!.ConcurrencyToken);
    }

    [Test]
    public async Task RefreshMember_AfterPictureDeleted_ClearsPictureVersion()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var user = (await _users.GetAsync(owner))!;
        await _pictures.AddAsync(new UserPicture { UserId = user.Id, Data = [1], ContentType = "image/png" });
        await grain.RefreshMemberAsync(owner);
        (await _pictures.DeleteAsync(p => p.UserId == user.Id)).IsSuccess.Should().BeTrue();

        await grain.RefreshMemberAsync(owner);
        var version = (await grain.GetSnapshotAsync()).Snapshot!.FindMember(owner)!.PictureVersion;

        version.Should().BeNull();
    }

    [Test]
    public async Task RefreshMember_UpdatesUserName()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var before = (await grain.GetSnapshotAsync()).Snapshot!;
        var user = (await _users.GetAsync(owner))!;
        user.UserName = "Renamed" + user.UserName[^6..];
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        (await _users.UpdateAsync(user)).IsSuccess.Should().BeTrue();

        await grain.RefreshMemberAsync(owner);
        var after = (await grain.GetSnapshotAsync()).Snapshot!;

        after.FindMember(owner)!.UserName.Should().Be(user.UserName);
        after.Version.Supersedes(before.Version).Should().BeTrue();
    }

    [Test]
    public async Task RefreshRates_ByAdmin_FollowsTheMarketKeepingLoss()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "USD", new(0.09m, 1.5m));
        await grain.SetExchangeRateAsync(owner, "EUR", new(0.1m));
        _rateProvider.Rates["USD"] = 0.095m;
        _rateProvider.Rates["EUR"] = 0.087m;

        var result = await grain.RefreshRatesAsync(owner);

        // Each rate follows the market while its conversion loss is kept, and a refresh forces fresh market data.
        var refreshed = result.Snapshot!;
        refreshed.ExchangeRates["USD"].Should().Be(new ExchangeRateSnapshot(0.095m, 1.5m));
        refreshed.ExchangeRates["EUR"].Should().Be(new ExchangeRateSnapshot(0.087m, 0));
        _rateProvider.LastForceRefresh.Should().BeTrue();
    }

    [Test]
    public async Task RefreshRates_ByNonAdmin_ReturnsForbidden()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetExchangeRateAsync(owner, "USD", new(0.09m, 1.5m));

        var result = await grain.RefreshRatesAsync(member);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task RemoveCurrency_AfterItsExpenseIsDeleted_RemovesCurrency()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "JPY", new(null));
        var yen = await AddExpenseAsync(grain, owner, Dinner(owner, owner) with { Currency = "JPY" });
        await grain.DeleteExpenseAsync(owner, yen);

        var result = await grain.RemoveCurrencyAsync(owner, "JPY");

        result.Snapshot!.ExchangeRates.Should().BeEmpty();
    }

    [Test]
    public async Task RemoveCurrency_ByAdmin_RemovesCurrency()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "EUR", new(11.25m, 2.5m));
        await grain.SetExchangeRateAsync(owner, "USD", new(10));

        var result = await grain.RemoveCurrencyAsync(owner, "USD");

        result.Snapshot!.ExchangeRates.Keys.Should().Equal("EUR");
    }

    [Test]
    public async Task RemoveCurrency_ByNonAdmin_ReturnsForbidden()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetExchangeRateAsync(owner, "USD", new(10));

        var result = await grain.RemoveCurrencyAsync(member, "USD");

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task RemoveCurrency_UsedByAnExpense_ReturnsInvalid()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "JPY", new(null));
        await AddExpenseAsync(grain, owner, Dinner(owner, owner) with { Currency = "JPY" });

        var result = await grain.RemoveCurrencyAsync(owner, "JPY");

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task RemoveCurrency_WhenAlreadyRemoved_ReturnsNotFound()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "JPY", new(null));
        await grain.RemoveCurrencyAsync(owner, "JPY");

        var result = await grain.RemoveCurrencyAsync(owner, "JPY");

        result.Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task RemoveTimeOption_ByAdminWhileNewTimesClosed_RemovesTime()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetAllowNewTimesAsync(owner, true);
        var option = await AddOptionAsync(grain, member, 18);
        await AddOptionAsync(grain, owner, 20);
        await grain.SetAllowNewTimesAsync(owner, false);

        var result = await grain.RemoveTimeOptionAsync(owner, option);

        result.Snapshot!.TimeOptions.Should().ContainSingle();
    }

    [Test]
    public async Task RemoveTimeOption_ByMemberAfterNewTimesReopened_RemovesTime()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetAllowNewTimesAsync(owner, true);
        var option = await AddOptionAsync(grain, owner, 20);
        await grain.SetAllowNewTimesAsync(owner, false);
        await grain.SetAllowNewTimesAsync(owner, true);

        var result = await grain.RemoveTimeOptionAsync(member, option);

        result.Snapshot!.TimeOptions.Should().BeEmpty();
    }

    [Test]
    public async Task RemoveTimeOption_ByMemberWhileNewTimesClosed_ReturnsForbidden()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetAllowNewTimesAsync(owner, true);
        var option = await AddOptionAsync(grain, member, 18);
        await grain.SetAllowNewTimesAsync(owner, false);

        var result = await grain.RemoveTimeOptionAsync(member, option);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task RemoveTimeOption_ClearsThePickAndKeepsTheArrangementWritable()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.SetVoteAsync(owner, option, VoteAnswer.Yes);
        await grain.PickTimeAsync(owner, option);

        var removed = (await grain.RemoveTimeOptionAsync(owner, option)).Snapshot!;
        var closed = (await grain.SetTabsAsync(owner, new SetTabsRequest(true, false))).Snapshot!;

        removed.TimeOptions.Should().BeEmpty();
        removed.FinalTimeOptionId.Should().BeNull();
        closed.IsScheduleDisabled.Should().BeTrue();
        closed.FinalTimeOptionId.Should().BeNull();
        await _cluster.DeactivateAsync(grain);
        (await grain.GetSnapshotAsync()).Snapshot!.Should().BeEquivalentTo(closed, o => o.Excluding(s => s.Version));
    }

    [Test]
    public async Task RemoveTimeOption_DeletesItsVotes()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.SetVoteAsync(owner, option, VoteAnswer.Yes);

        var result = await grain.RemoveTimeOptionAsync(owner, option);

        result.Snapshot!.TimeOptions.Should().BeEmpty();
        await _cluster.DeactivateAsync(grain);
        (await grain.GetSnapshotAsync()).Snapshot!.TimeOptions.Should().BeEmpty();
    }

    [Test]
    public async Task RemoveTimeOption_WhenAlreadyRemoved_ReturnsNotFound()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.RemoveTimeOptionAsync(owner, option);

        var result = await grain.RemoveTimeOptionAsync(owner, option);

        result.Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task Schedule_SurvivesReactivation()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetAllowNewTimesAsync(owner, true);
        var late = await AddOptionAsync(grain, owner, 20);
        var early = await AddOptionAsync(grain, member, 18);
        await grain.SetVoteAsync(member, late, VoteAnswer.No);
        await grain.SetVoteAsync(owner, early, VoteAnswer.Yes);
        await grain.PickTimeAsync(owner, early);
        var before = (await grain.SetTabsAsync(owner, new SetTabsRequest(true, false))).Snapshot!;

        await _cluster.DeactivateAsync(grain);
        var after = (await grain.GetSnapshotAsync()).Snapshot!;

        before.TimeOptions.Select(o => o.Id).Should().Equal(early, late);
        after.Should().BeEquivalentTo(before, o => o.Excluding(s => s.Version).WithStrictOrdering());
    }

    [TestCaseSource(nameof(ScheduleCommands))]
    public async Task ScheduleCommand_WhileScheduleDisabled_ReturnsForbidden(Func<IArrangementGrain, Guid, Guid, long, Task<CommandResult>> command)
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.SetTabsAsync(owner, new SetTabsRequest(true, false));

        var result = await command(grain, owner, member, option);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task SetAdmin_ByNonOwner_ReturnsForbidden()
    {
        var (grain, _, member) = await CreateWithMemberAsync();

        var result = await grain.SetAdminAsync(member, member, true);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task SetAdmin_ByOutsider_ReturnsNotMember()
    {
        var (grain, _, member) = await CreateWithMemberAsync();
        var outsider = await AddUserAsync("Outsider");

        var result = await grain.SetAdminAsync(outsider, member, true);

        result.Error.Should().Be(CommandError.NotMember);
    }

    [Test]
    public async Task SetAdmin_ByOwner_PromotesMember()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();

        var result = await grain.SetAdminAsync(owner, member, true);

        result.Snapshot!.FindMember(member)!.IsAdmin.Should().BeTrue();
    }

    [Test]
    public async Task SetAdmin_OfOutsider_ReturnsNotFound()
    {
        var owner = await AddUserAsync("Owner");
        var outsider = await AddUserAsync("Outsider");
        var grain = await CreateAsync(owner);

        var result = await grain.SetAdminAsync(owner, outsider, true);

        result.Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task SetAdmin_RevokingOwner_ReturnsInvalid()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.SetAdminAsync(owner, owner, false);

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task SetAllowNewTimes_ByAdmin_OpensNewTimes()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.SetAllowNewTimesAsync(owner, true);

        result.Snapshot!.AllowNewTimes.Should().BeTrue();
    }

    [Test]
    public async Task SetAllowNewTimes_ByNonAdmin_ReturnsForbidden()
    {
        var (grain, _, member) = await CreateWithMemberAsync();

        var result = await grain.SetAllowNewTimesAsync(member, true);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task SetAllowNewTimes_ClosingAgain_LocksMembersOutButNotAdmins()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetAllowNewTimesAsync(owner, true);

        var closed = await grain.SetAllowNewTimesAsync(owner, false);
        var byMember = await grain.AddTimeOptionAsync(member, Timed(22));
        var byAdmin = await grain.AddTimeOptionAsync(owner, Timed(22));

        closed.Snapshot!.AllowNewTimes.Should().BeFalse();
        byMember.Error.Should().Be(CommandError.Forbidden);
        byAdmin.Snapshot!.TimeOptions.Should().HaveCount(1);
    }

    [Test]
    public async Task SetBaseCurrency_ByAdmin_ClearsTheRates()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "EUR", new(11.25m, 2.5m));

        var result = await grain.SetBaseCurrencyAsync(owner, "EUR");

        // The old base stays available without a rate.
        var unrated = new Dictionary<string, ExchangeRateSnapshot> { ["SEK"] = new(null, 0) };
        result.Snapshot!.BaseCurrency.Should().Be("EUR");
        result.Snapshot.ExchangeRates.Should().BeEquivalentTo(unrated);
        await _cluster.DeactivateAsync(grain);
        (await grain.GetSnapshotAsync()).Snapshot!.ExchangeRates.Should().BeEquivalentTo(unrated);
    }

    [Test]
    public async Task SetBaseCurrency_ByNonAdmin_ReturnsForbidden()
    {
        var (grain, _, member) = await CreateWithMemberAsync();

        var result = await grain.SetBaseCurrencyAsync(member, "EUR");

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task SetBaseCurrency_WithUnknownCurrency_ReturnsInvalid()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.SetBaseCurrencyAsync(owner, "XXX");

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task SetExchangeRate_AfterRefreshRates_IsAcceptedAndSaved()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "USD", new(0.09m, 1.5m));
        await grain.SetExchangeRateAsync(owner, "EUR", new(0.1m));
        _rateProvider.Rates["USD"] = 0.095m;
        _rateProvider.Rates["EUR"] = 0.087m;
        await grain.RefreshRatesAsync(owner);

        var result = await grain.SetExchangeRateAsync(owner, "EUR", new(0.087m, 2m));

        // The refreshed rows' revisions are current, so a later change is accepted, and the refresh was saved.
        result.IsSuccess.Should().BeTrue();
        await _cluster.DeactivateAsync(grain);
        var reloaded = (await grain.GetSnapshotAsync()).Snapshot!;
        reloaded.ExchangeRates["USD"].Should().Be(new ExchangeRateSnapshot(0.095m, 1.5m));
        reloaded.ExchangeRates["EUR"].Should().Be(new ExchangeRateSnapshot(0.087m, 2m));
    }

    [Test]
    public async Task SetExchangeRate_ByAdmin_AddsCurrency()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.SetExchangeRateAsync(owner, "EUR", new(11.5m));

        result.Snapshot!.ExchangeRates.Should().Equal(new Dictionary<string, ExchangeRateSnapshot> { ["EUR"] = new(11.5m, 0) });
    }

    [Test]
    public async Task SetExchangeRate_ByNonAdmin_ReturnsForbidden()
    {
        var (grain, _, member) = await CreateWithMemberAsync();

        var result = await grain.SetExchangeRateAsync(member, "EUR", new(11.5m));

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [TestCaseSource(nameof(InvalidExchangeRates))]
    public async Task SetExchangeRate_WithInvalidRate_ReturnsInvalid(string currency, SetExchangeRateRequest request)
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.SetExchangeRateAsync(owner, currency, request);

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task SetExchangeRate_WithoutRate_ClearsTheRate()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "USD", new(10));
        // Reloaded, so the change applies to the stored rate.
        await _cluster.DeactivateAsync(grain);

        var result = await grain.SetExchangeRateAsync(owner, "USD", new(null));

        result.Snapshot!.ExchangeRates["USD"].Should().Be(new ExchangeRateSnapshot(null, 0));
    }

    [Test]
    public async Task SetExchangeRate_WithoutRateForNewCurrency_SeedsTheMarketRate()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        _rateProvider.Rates["USD"] = 0.095m;

        var result = await grain.SetExchangeRateAsync(owner, "USD", new(null));

        result.Snapshot!.ExchangeRates["USD"].Should().Be(new ExchangeRateSnapshot(0.095m, 0));
    }

    [Test]
    public async Task SetExchangeRate_WithRateForNewCurrency_KeepsTheGivenRate()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        _rateProvider.Rates["EUR"] = 0.087m;

        var result = await grain.SetExchangeRateAsync(owner, "EUR", new(0.1m));

        result.Snapshot!.ExchangeRates["EUR"].Should().Be(new ExchangeRateSnapshot(0.1m, 0));
    }

    [Test]
    public async Task SetExchangeRate_WithSameRate_KeepsVersion()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        await grain.SetExchangeRateAsync(owner, "EUR", new(11.5m));
        var updated = (await grain.SetExchangeRateAsync(owner, "EUR", new(11.25m, 2.5m))).Snapshot!;

        var result = await grain.SetExchangeRateAsync(owner, "EUR", new(11.25m, 2.5m));

        result.Snapshot!.Version.Should().Be(updated.Version);
    }

    [Test]
    public async Task SetParticipationOpen_ByAdmin_ClosesParticipation()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.SetParticipationOpenAsync(owner, false);

        result.Snapshot!.AllowNewUsers.Should().BeFalse();
    }

    [Test]
    public async Task SetParticipationOpen_ByNonAdmin_ReturnsForbidden()
    {
        var (grain, _, member) = await CreateWithMemberAsync();

        var result = await grain.SetParticipationOpenAsync(member, false);

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task SetTabs_ByAdmin_DisablesScheduleAndEnablesExpenses()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner, Request with { IsExpensesDisabled = true });

        var result = await grain.SetTabsAsync(owner, new SetTabsRequest(true, false));

        result.Snapshot!.IsScheduleDisabled.Should().BeTrue();
        result.Snapshot.IsExpensesDisabled.Should().BeFalse();
    }

    [Test]
    public async Task SetTabs_ByNonAdmin_ReturnsForbidden()
    {
        var (grain, _, member) = await CreateWithMemberAsync();

        var result = await grain.SetTabsAsync(member, new SetTabsRequest(true, false));

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task SetTabs_ClosingSchedule_KeepsPick()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.PickTimeAsync(owner, option);

        var result = await grain.SetTabsAsync(owner, new SetTabsRequest(true, false));

        result.Snapshot!.FinalTimeOptionId.Should().Be(option);
    }

    [Test]
    public async Task SetTabs_ReopeningSchedule_KeepsPick()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.PickTimeAsync(owner, option);
        await grain.SetTabsAsync(owner, new SetTabsRequest(true, false));

        var result = await grain.SetTabsAsync(owner, new SetTabsRequest(false, false));

        result.Snapshot!.FinalTimeOptionId.Should().Be(option);
    }

    [Test]
    public async Task SetTabs_WithSameTabs_KeepsVersion()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var disabled = (await grain.SetTabsAsync(owner, new SetTabsRequest(true, false))).Snapshot!;

        var result = await grain.SetTabsAsync(owner, new SetTabsRequest(true, false));

        result.Snapshot!.Version.Should().Be(disabled.Version);
    }

    [Test]
    public async Task SetVote_AfterScheduleReopened_AddsVote()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.PickTimeAsync(owner, option);
        await grain.SetTabsAsync(owner, new SetTabsRequest(true, false));
        await grain.SetTabsAsync(owner, new SetTabsRequest(false, false));

        var result = await grain.SetVoteAsync(member, option, VoteAnswer.Yes);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task SetVote_ByNonMember_ReturnsNotMember()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);

        var result = await grain.SetVoteAsync(Guid.NewGuid(), option, VoteAnswer.No);

        result.Error.Should().Be(CommandError.NotMember);
    }

    [Test]
    public async Task SetVote_OnMissingOption_ReturnsNotFound()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);

        var result = await grain.SetVoteAsync(owner, option + 1000, VoteAnswer.No);

        result.Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task SetVote_WithNewAnswer_ReplacesVote()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.SetVoteAsync(owner, option, VoteAnswer.Yes);
        await grain.SetVoteAsync(member, option, VoteAnswer.Yes);

        var result = await grain.SetVoteAsync(member, option, VoteAnswer.Maybe);

        var changed = result.Snapshot!.TimeOptions[0];
        changed.Count(VoteAnswer.Yes).Should().Be(1);
        changed.FindAnswer(member).Should().Be(VoteAnswer.Maybe);
    }

    [Test]
    public async Task SetVote_WithSameAnswer_KeepsVersion()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);
        var first = (await grain.SetVoteAsync(owner, option, VoteAnswer.Yes)).Snapshot!;

        var result = await grain.SetVoteAsync(owner, option, VoteAnswer.Yes);

        result.Snapshot!.Version.Should().Be(first.Version);
    }

    [Test]
    public async Task SetVote_WithUnknownAnswer_ReturnsInvalid()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);

        var result = await grain.SetVoteAsync(owner, option, (VoteAnswer)42);

        result.Error.Should().Be(CommandError.Invalid);
    }

    [Test]
    public async Task State_SurvivesReactivation()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        var before = (await grain.SetAdminAsync(owner, member, true)).Snapshot!;

        await _cluster.DeactivateAsync(grain);
        var after = (await grain.GetSnapshotAsync()).Snapshot!;

        after.Should().BeEquivalentTo(before, o => o.Excluding(s => s.Version));
        after.Version.Supersedes(before.Version).Should().BeTrue();
    }

    [Test]
    public async Task UpdateDetails_ByPromotedAdmin_ChangesTitle()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        await grain.SetAdminAsync(owner, member, true);

        var result = await grain.UpdateDetailsAsync(member, Details("By admin"));

        result.Snapshot!.Title.Should().Be("By admin");
    }

    [Test]
    public async Task UpdateDetails_WithNewDetails_AdvancesVersion()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var before = (await grain.GetSnapshotAsync()).Snapshot!.Version;

        var result = await grain.UpdateDetailsAsync(owner, Details("Renamed"));

        result.Snapshot!.Version.Supersedes(before).Should().BeTrue();
    }

    [Test]
    public async Task UpdateDetails_WithSameDetails_KeepsVersion()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var before = (await grain.GetSnapshotAsync()).Snapshot!.Version;

        var result = await grain.UpdateDetailsAsync(owner, Details(Request.Title));

        result.Snapshot!.Version.Should().Be(before);
    }

    [Test]
    public async Task UpdateExpense_ByAdminNotPayer_ReplacesExpense()
    {
        var (grain, owner, payer) = await CreateWithMemberAsync();
        var other = await AddUserAsync("Other");
        await grain.JoinAsync(other);
        var id = await AddExpenseAsync(grain, payer, Dinner(payer, payer, other));

        var result = await grain.UpdateExpenseAsync(owner, id, Dinner(payer, payer, other) with { Amount = 15m });

        result.Snapshot!.Expenses.Single().Amount.Should().Be(15m);
    }

    [Test]
    public async Task UpdateExpense_ByNonPayerNonAdmin_ReturnsForbidden()
    {
        var (grain, _, payer) = await CreateWithMemberAsync();
        var other = await AddUserAsync("Other");
        await grain.JoinAsync(other);
        var id = await AddExpenseAsync(grain, payer, Dinner(payer, payer, other));

        var result = await grain.UpdateExpenseAsync(other, id, Dinner(payer, payer, other) with { Amount = 12m });

        result.Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task UpdateExpense_ByPayer_ReplacesExpense()
    {
        var (grain, _, payer) = await CreateWithMemberAsync();
        var other = await AddUserAsync("Other");
        await grain.JoinAsync(other);
        var id = await AddExpenseAsync(grain, payer, Dinner(payer, payer, other));

        var result = await grain.UpdateExpenseAsync(payer, id, Dinner(payer, payer, other) with { Amount = 12m });

        result.Snapshot!.Expenses.Single().Amount.Should().Be(12m);
    }

    [Test]
    public async Task UpdateExpense_KeepsIdAndReplacesFields()
    {
        var (grain, owner, member) = await CreateWithMemberAsync();
        var third = await AddUserAsync("Third");
        await grain.JoinAsync(third);
        var id = await AddExpenseAsync(grain, member, Dinner(owner, owner, member, third));

        var result = await grain.UpdateExpenseAsync(owner, id, Dinner(member, member, third) with { Amount = 31.5m });

        var replaced = result.Snapshot!.Expenses.Single();
        replaced.Id.Should().Be(id);
        replaced.Amount.Should().Be(31.5m);
        replaced.PaidByUserId.Should().Be(member);
        replaced.ParticipantUserIds.Should().BeEquivalentTo([member, third]);
    }

    [Test]
    public async Task UpdateExpense_OfMissingExpense_ReturnsNotFound()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var id = await AddExpenseAsync(grain, owner, Dinner(owner, owner));

        var result = await grain.UpdateExpenseAsync(owner, id + 1000, Dinner(owner, owner));

        result.Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task WriteRefusedByDatabase_ReturnsConflictAndReloads()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        (await _arrangements.DeleteAsync(grain.GetPrimaryKey())).IsSuccess.Should().BeTrue();

        var stale = await grain.UpdateDetailsAsync(owner, Details("Too late"));

        stale.Error.Should().Be(CommandError.Conflict);
        await _cluster.WaitForDeactivationAsync(grain);
        (await grain.GetSnapshotAsync()).Error.Should().Be(CommandError.NotFound);
    }

    private static async Task<long> AddExpenseAsync(IArrangementGrain grain, Guid userId, ExpenseRequest request)
    {
        var result = await grain.AddExpenseAsync(userId, request);
        return result.Snapshot!.Expenses.Max(e => e.Id);
    }

    private static async Task<long> AddOptionAsync(IArrangementGrain grain, Guid userId, int hour)
    {
        var result = await grain.AddTimeOptionAsync(userId, Timed(hour));
        return result.Snapshot!.TimeOptions.Single(o => o.DateBegin == Evening.AddHours(hour)).Id;
    }

    private async Task<Guid> AddUserAsync(string prefix)
    {
        var userName = prefix + Guid.NewGuid().ToString("N")[..6];
        var user = new User
        {
            UniqueId = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            PasswordHash = "not used",
            DefaultCurrency = "SEK",
        };

        var added = await _users.AddAsync(user, u => u.NormalizedUserName == user.NormalizedUserName);
        added.IsSuccess.Should().BeTrue();
        return added.Value!.UniqueId;
    }

    private async Task<IArrangementGrain> CreateAsync(Guid owner, CreateArrangementRequest? request = null)
    {
        var grain = NewGrain();
        (await grain.CreateAsync(owner, request ?? Request)).IsSuccess.Should().BeTrue();
        return grain;
    }

    private async Task<(IArrangementGrain Grain, Guid Owner, Guid Member)> CreateWithMemberAsync()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var grain = await CreateAsync(owner);
        (await grain.JoinAsync(member)).IsSuccess.Should().BeTrue();
        return (grain, owner, member);
    }

    private static UpdateArrangementDetailsRequest Details(string title)
    {
        return new UpdateArrangementDetailsRequest(title, Request.Description);
    }

    private static ExpenseRequest Dinner(Guid paidBy, params Guid[] participants)
    {
        return new ExpenseRequest(" Dinner ", 30m, "SEK", paidBy, participants, new DateOnly(2026, 6, 19), false);
    }

    private static TestCaseData ExpenseCommandCase(string name, Func<IArrangementGrain, Guid, long, Task<CommandResult>> command)
    {
        return new TestCaseData(command).SetArgDisplayNames(name);
    }

    private static TestCaseData InvalidExpenseCase(string name, Func<Guid, Guid, ExpenseRequest> request)
    {
        return new TestCaseData(request).SetArgDisplayNames(name);
    }

    private static TestCaseData MissingArrangementCase(string name, Func<IArrangementGrain, Guid, Task<CommandResult>> command)
    {
        return new TestCaseData(command).SetArgDisplayNames(name);
    }

    private IArrangementGrain NewGrain()
    {
        return _cluster.Client.GetGrain<IArrangementGrain>(Guid.NewGuid());
    }

    private static TestCaseData ScheduleCommandCase(string name, Func<IArrangementGrain, Guid, Guid, long, Task<CommandResult>> command)
    {
        return new TestCaseData(command).SetArgDisplayNames(name);
    }

    private static AddTimeOptionRequest Timed(int hour)
    {
        return new AddTimeOptionRequest(Evening.AddHours(hour), Evening.AddHours(hour + 3), false);
    }
}
