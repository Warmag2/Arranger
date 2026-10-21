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
using RepositoryPrototype.Interfaces;

namespace Faemiyah.Arranger.IntegrationTests;

[TestFixture]
public class ArrangementGrainTests
{
    private static readonly CreateArrangementRequest Request = new("Sauna evening", "Bring towels.");
    private static readonly DateTime Evening = new(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);

    private readonly RecordingNotifier _notifier = new();
    private InProcessTestCluster _cluster = null!;
    private IEntityRepositoryWithUuid<long, User> _users = null!;
    private IEntityRepositoryWithUuid<long, Arrangement> _arrangements = null!;

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
        });

        _cluster = builder.Build();
        await _cluster.DeployAsync();
        var services = _cluster.GetSiloServiceProvider(_cluster.Silos[0].SiloAddress);
        _users = services.GetRequiredService<IEntityRepositoryWithUuid<long, User>>();
        _arrangements = services.GetRequiredService<IEntityRepositoryWithUuid<long, Arrangement>>();
    }

    [OneTimeTearDown]
    public async Task StopClusterAsync()
    {
        await _cluster.DisposeAsync();
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
        snapshot.Members.Should().ContainSingle().Which.Should().Match<ArrangementMemberSnapshot>(m => m.UserId == owner && m.IsAdmin);
    }

    [Test]
    public async Task Create_RefusesExistingAndInvalid()
    {
        var owner = await AddUserAsync("Owner");
        var grain = NewGrain();

        (await grain.CreateAsync(owner, Request with { Title = " " })).Error.Should().Be(CommandError.Invalid);
        (await grain.CreateAsync(Guid.NewGuid(), Request)).Error.Should().Be(CommandError.NotFound);
        (await grain.CreateAsync(owner, Request)).IsSuccess.Should().BeTrue();
        (await grain.CreateAsync(owner, Request)).Error.Should().Be(CommandError.Conflict);
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
        await grain.GetSnapshotAsync(member);
        await grain.DeleteAsync(owner);

        var notifications = _notifier.For(grain.GetPrimaryKey());
        notifications.Should().HaveCount(3);
        notifications[0]!.Members.Should().HaveCount(1);
        notifications[1]!.Members.Should().HaveCount(2);
        notifications[1]!.Version.Supersedes(notifications[0]!.Version).Should().BeTrue();
        notifications[2].Should().BeNull();
    }

    [Test]
    public async Task Commands_OnMissingArrangement_ReturnNotFound()
    {
        var user = await AddUserAsync("Someone");
        var grain = NewGrain();

        (await grain.GetSnapshotAsync(user)).Error.Should().Be(CommandError.NotFound);
        (await grain.JoinAsync(user)).Error.Should().Be(CommandError.NotFound);
        (await grain.SetParticipationOpenAsync(user, false)).Error.Should().Be(CommandError.NotFound);
        (await grain.DeleteAsync(user)).Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task Join_FailsAfterAdminClosesParticipation()
    {
        var owner = await AddUserAsync("Owner");
        var early = await AddUserAsync("Early");
        var late = await AddUserAsync("Late");
        var grain = await CreateAsync(owner);

        (await grain.JoinAsync(early)).Snapshot!.Members.Should().HaveCount(2);
        (await grain.JoinAsync(early)).Snapshot!.Members.Should().HaveCount(2);
        (await grain.SetParticipationOpenAsync(early, false)).Error.Should().Be(CommandError.Forbidden);
        (await grain.SetParticipationOpenAsync(owner, false)).Snapshot!.IsParticipationClosed.Should().BeTrue();

        var refused = await grain.JoinAsync(late);

        refused.Error.Should().Be(CommandError.Forbidden);
        (await grain.GetSnapshotAsync(late)).Snapshot!.FindMember(late).Should().BeNull();
    }

    [Test]
    public async Task SetAdmin_OnlyOwnerChangesAdminsOfOthers()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var outsider = await AddUserAsync("Outsider");
        var grain = await CreateAsync(owner);
        await grain.JoinAsync(member);

        (await grain.SetAdminAsync(outsider, member, true)).Error.Should().Be(CommandError.NotMember);
        (await grain.SetAdminAsync(member, member, true)).Error.Should().Be(CommandError.Forbidden);
        (await grain.SetAdminAsync(owner, outsider, true)).Error.Should().Be(CommandError.NotFound);
        (await grain.SetAdminAsync(owner, owner, false)).Error.Should().Be(CommandError.Invalid);

        var promoted = await grain.SetAdminAsync(owner, member, true);

        promoted.Snapshot!.FindMember(member)!.IsAdmin.Should().BeTrue();
        (await grain.UpdateDetailsAsync(member, Details("By admin"))).Snapshot!.Title.Should().Be("By admin");
        (await grain.DeleteAsync(member)).Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task Commands_AdvanceVersionOnlyOnChange()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var before = (await grain.GetSnapshotAsync(owner)).Snapshot!.Version;

        var unchanged = (await grain.UpdateDetailsAsync(owner, Details(Request.Title))).Snapshot!.Version;
        var changed = (await grain.UpdateDetailsAsync(owner, Details("Renamed"))).Snapshot!.Version;

        unchanged.Should().Be(before);
        changed.Supersedes(before).Should().BeTrue();
    }

    [Test]
    public async Task State_SurvivesReactivation()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var grain = await CreateAsync(owner);
        await grain.JoinAsync(member);
        var before = (await grain.SetAdminAsync(owner, member, true)).Snapshot!;

        await _cluster.DeactivateAsync(grain);
        var after = (await grain.GetSnapshotAsync(owner)).Snapshot!;

        after.Should().BeEquivalentTo(before, o => o.Excluding(s => s.Version));
        after.Version.Supersedes(before.Version).Should().BeTrue();
    }

    [Test]
    public async Task Delete_RemovesArrangement()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);

        var result = await grain.DeleteAsync(owner);

        result.IsSuccess.Should().BeTrue();
        result.Snapshot.Should().BeNull();
        (await grain.GetSnapshotAsync(owner)).Error.Should().Be(CommandError.NotFound);
        (await _arrangements.GetAsync(a => a.UniqueId == grain.GetPrimaryKey())).Should().BeNull();
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
        (await grain.GetSnapshotAsync(owner)).Error.Should().Be(CommandError.NotFound);
    }

    [Test]
    public async Task RefreshMember_UpdatesHandle()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var before = (await grain.GetSnapshotAsync(owner)).Snapshot!;
        var user = (await _users.GetAsync(u => u.UniqueId == owner))!;
        user.UserName = "Renamed" + user.UserName[^6..];
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        (await _users.UpdateAsync(user)).IsSuccess.Should().BeTrue();

        await grain.RefreshMemberAsync(owner);
        var after = (await grain.GetSnapshotAsync(owner)).Snapshot!;

        after.FindMember(owner)!.UserName.Should().Be(user.UserName);
        after.Version.Supersedes(before.Version).Should().BeTrue();
    }

    [Test]
    public async Task TimeOptions_AreValidatedAndUnique()
    {
        var owner = await AddUserAsync("Owner");
        var outsider = await AddUserAsync("Outsider");
        var grain = await CreateAsync(owner);

        var added = await grain.AddTimeOptionAsync(owner, Timed(18) with { DateBegin = Evening.AddHours(18).AddSeconds(30) });

        added.Snapshot!.TimeOptions.Should().ContainSingle().Which.DateBegin.Should().Be(Evening.AddHours(18));
        (await grain.AddTimeOptionAsync(owner, Timed(18))).Error.Should().Be(CommandError.Invalid);
        (await grain.AddTimeOptionAsync(owner, new AddTimeOptionRequest(Evening.AddHours(1), null, true))).Error.Should().Be(CommandError.Invalid);
        (await grain.AddTimeOptionAsync(outsider, Timed(20))).Error.Should().Be(CommandError.NotMember);
        (await grain.AddTimeOptionAsync(owner, new AddTimeOptionRequest(Evening, null, true))).Snapshot!.TimeOptions.Should().HaveCount(2);
    }

    [Test]
    public async Task SetVote_InsertsChangesAndSkipsSame()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var grain = await CreateAsync(owner);
        await grain.JoinAsync(member);
        var option = await AddOptionAsync(grain, owner, 18);

        await grain.SetVoteAsync(owner, option, VoteAnswer.Yes);
        var first = (await grain.SetVoteAsync(member, option, VoteAnswer.Yes)).Snapshot!;
        var same = (await grain.SetVoteAsync(member, option, VoteAnswer.Yes)).Snapshot!;
        var changed = (await grain.SetVoteAsync(member, option, VoteAnswer.Maybe)).Snapshot!.TimeOptions[0];

        same.Version.Should().Be(first.Version);
        changed.Count(VoteAnswer.Yes).Should().Be(1);
        changed.FindAnswer(member).Should().Be(VoteAnswer.Maybe);
        (await grain.SetVoteAsync(member, option, (VoteAnswer)42)).Error.Should().Be(CommandError.Invalid);
        (await grain.SetVoteAsync(member, option + 1000, VoteAnswer.No)).Error.Should().Be(CommandError.NotFound);
        (await grain.SetVoteAsync(Guid.NewGuid(), option, VoteAnswer.No)).Error.Should().Be(CommandError.NotMember);
    }

    [Test]
    public async Task RemoveTimeOption_DeletesItsVotes()
    {
        var owner = await AddUserAsync("Owner");
        var grain = await CreateAsync(owner);
        var option = await AddOptionAsync(grain, owner, 18);
        await grain.SetVoteAsync(owner, option, VoteAnswer.Yes);

        (await grain.RemoveTimeOptionAsync(owner, option)).Snapshot!.TimeOptions.Should().BeEmpty();
        (await grain.RemoveTimeOptionAsync(owner, option)).Error.Should().Be(CommandError.NotFound);

        await _cluster.DeactivateAsync(grain);
        (await grain.GetSnapshotAsync(owner)).Snapshot!.TimeOptions.Should().BeEmpty();
    }

    [Test]
    public async Task PickTime_IsKeptWhenScheduleIsClosedAndReopened()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var grain = await CreateAsync(owner);
        await grain.JoinAsync(member);
        var option = await AddOptionAsync(grain, owner, 18);

        (await grain.PickTimeAsync(member, option)).Error.Should().Be(CommandError.Forbidden);
        (await grain.PickTimeAsync(owner, option + 1000)).Error.Should().Be(CommandError.NotFound);
        (await grain.PickTimeAsync(owner, option)).Snapshot!.FinalTimeOptionId.Should().Be(option);
        (await grain.PickTimeAsync(owner, null)).Snapshot!.FinalTimeOptionId.Should().BeNull();
        var picked = (await grain.PickTimeAsync(owner, option)).Snapshot!;

        picked.IsScheduleDisabled.Should().BeFalse();
        var closed = (await grain.SetTabsAsync(owner, new SetTabsRequest(false, true))).Snapshot!;

        closed.FinalTimeOptionId.Should().Be(option);
        (await grain.PickTimeAsync(owner, null)).Error.Should().Be(CommandError.Forbidden);
        (await grain.SetVoteAsync(member, option, VoteAnswer.Yes)).Error.Should().Be(CommandError.Forbidden);

        var reopened = (await grain.SetTabsAsync(owner, new SetTabsRequest(true, true))).Snapshot!;

        reopened.FinalTimeOptionId.Should().Be(option);
        (await grain.SetVoteAsync(member, option, VoteAnswer.Yes)).IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task Schedule_SurvivesReactivation()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var grain = await CreateAsync(owner);
        await grain.JoinAsync(member);
        var late = await AddOptionAsync(grain, owner, 20);
        var early = await AddOptionAsync(grain, member, 18);
        await grain.SetVoteAsync(member, late, VoteAnswer.No);
        await grain.SetVoteAsync(owner, early, VoteAnswer.Yes);
        await grain.PickTimeAsync(owner, early);
        var before = (await grain.SetTabsAsync(owner, new SetTabsRequest(false, true))).Snapshot!;

        await _cluster.DeactivateAsync(grain);
        var after = (await grain.GetSnapshotAsync(owner)).Snapshot!;

        before.TimeOptions.Select(o => o.Id).Should().Equal(early, late);
        after.Should().BeEquivalentTo(before, o => o.Excluding(s => s.Version).WithStrictOrdering());
    }

    [Test]
    public async Task DisabledSchedule_RefusesChanges()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var grain = NewGrain();
        var created = (await grain.CreateAsync(owner, Request with { IsExpensesEnabled = false })).Snapshot!;
        await grain.JoinAsync(member);
        var option = await AddOptionAsync(grain, owner, 18);

        created.IsExpensesDisabled.Should().BeTrue();
        (await grain.SetTabsAsync(member, new SetTabsRequest(false, true))).Error.Should().Be(CommandError.Forbidden);
        var disabled = (await grain.SetTabsAsync(owner, new SetTabsRequest(false, true))).Snapshot!;

        disabled.IsScheduleDisabled.Should().BeTrue();
        disabled.IsExpensesDisabled.Should().BeFalse();
        (await grain.SetTabsAsync(owner, new SetTabsRequest(false, true))).Snapshot!.Version.Should().Be(disabled.Version);
        (await grain.SetVoteAsync(member, option, VoteAnswer.Yes)).Error.Should().Be(CommandError.Forbidden);
        (await grain.AddTimeOptionAsync(member, Timed(20))).Error.Should().Be(CommandError.Forbidden);
        (await grain.RemoveTimeOptionAsync(owner, option)).Error.Should().Be(CommandError.Forbidden);
        (await grain.PickTimeAsync(owner, option)).Error.Should().Be(CommandError.Forbidden);

        await grain.SetTabsAsync(owner, new SetTabsRequest(true, true));
        (await grain.SetVoteAsync(member, option, VoteAnswer.Yes)).IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task Expenses_AreAddedReplacedAndDeleted_AndSurviveReactivation()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var third = await AddUserAsync("Third");
        var grain = await CreateAsync(owner);
        await grain.JoinAsync(member);
        await grain.JoinAsync(third);

        var added = (await grain.AddExpenseAsync(member, Dinner(owner, owner, member, third))).Snapshot!.Expenses.Single();

        added.Description.Should().Be("Dinner");
        added.PaidByUserId.Should().Be(owner);
        added.ParticipantUserIds.Should().BeEquivalentTo([owner, member, third]);

        var replaced = (await grain.UpdateExpenseAsync(third, added.Id, Dinner(member, member, third) with { Amount = 31.5m })).Snapshot!.Expenses.Single();

        replaced.Id.Should().Be(added.Id);
        replaced.Amount.Should().Be(31.5m);
        replaced.PaidByUserId.Should().Be(member);
        replaced.ParticipantUserIds.Should().BeEquivalentTo([member, third]);

        var payment = ExpenseMath.SuggestPayments([replaced], replaced.Date).Single();
        var before = (await grain.AddExpenseAsync(third, payment)).Snapshot!;

        ExpenseMath.SuggestPayments(before.Expenses, replaced.Date).Should().BeEmpty();
        before.Expenses.Select(e => e.IsSettlementPayment).Should().Equal(false, true);

        await _cluster.DeactivateAsync(grain);
        var after = (await grain.GetSnapshotAsync(owner)).Snapshot!;

        after.Should().BeEquivalentTo(before, o => o.Excluding(s => s.Version).WithStrictOrdering());
        (await grain.DeleteExpenseAsync(owner, added.Id)).Snapshot!.Expenses.Should().ContainSingle();
        (await grain.DeleteExpenseAsync(owner, added.Id)).Error.Should().Be(CommandError.NotFound);

        await _cluster.DeactivateAsync(grain);
        (await grain.GetSnapshotAsync(owner)).Snapshot!.Expenses.Should().ContainSingle().Which.IsSettlementPayment.Should().BeTrue();
    }

    [Test]
    public async Task Expenses_AreValidated_AndRefusedWhenDisabled()
    {
        var owner = await AddUserAsync("Owner");
        var outsider = await AddUserAsync("Outsider");
        var grain = await CreateAsync(owner);
        var id = (await grain.AddExpenseAsync(owner, Dinner(owner, owner))).Snapshot!.Expenses.Single().Id;

        (await grain.AddExpenseAsync(outsider, Dinner(outsider, outsider))).Error.Should().Be(CommandError.NotMember);
        (await grain.AddExpenseAsync(owner, Dinner(owner, owner, outsider))).Error.Should().Be(CommandError.Invalid);
        (await grain.AddExpenseAsync(owner, Dinner(owner))).Error.Should().Be(CommandError.Invalid);
        (await grain.AddExpenseAsync(owner, Dinner(owner, owner) with { Amount = 0 })).Error.Should().Be(CommandError.Invalid);
        (await grain.AddExpenseAsync(owner, Dinner(owner, owner) with { Currency = "JPY" })).Error.Should().Be(CommandError.Invalid);
        await grain.SetExchangeRateAsync(owner, "JPY", new(null));
        (await grain.AddExpenseAsync(owner, Dinner(owner, owner) with { Currency = "JPY", Amount = 1.5m })).Error.Should().Be(CommandError.Invalid);
        var yen = (await grain.AddExpenseAsync(owner, Dinner(owner, owner) with { Currency = "JPY" })).Snapshot!.Expenses.Single(e => e.Currency == "JPY").Id;
        (await grain.RemoveCurrencyAsync(owner, "JPY")).Error.Should().Be(CommandError.Invalid);
        await grain.DeleteExpenseAsync(owner, yen);
        (await grain.RemoveCurrencyAsync(owner, "JPY")).Snapshot!.ExchangeRates.Should().BeEmpty();
        (await grain.RemoveCurrencyAsync(owner, "JPY")).Error.Should().Be(CommandError.NotFound);
        (await grain.AddExpenseAsync(owner, Dinner(owner, owner) with { IsSettlementPayment = true })).Error.Should().Be(CommandError.Invalid);
        (await grain.UpdateExpenseAsync(owner, id + 1000, Dinner(owner, owner))).Error.Should().Be(CommandError.NotFound);

        await grain.SetTabsAsync(owner, new SetTabsRequest(true, false));

        (await grain.AddExpenseAsync(owner, Dinner(owner, owner))).Error.Should().Be(CommandError.Forbidden);
        (await grain.UpdateExpenseAsync(owner, id, Dinner(owner, owner))).Error.Should().Be(CommandError.Forbidden);
        (await grain.DeleteExpenseAsync(owner, id)).Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task ExchangeRates_AreSetRemovedAndClearedByBaseCurrency()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var grain = await CreateAsync(owner);
        await grain.JoinAsync(member);

        (await grain.SetExchangeRateAsync(member, "SEK", new(1))).Error.Should().Be(CommandError.Invalid);
        (await grain.SetExchangeRateAsync(member, "EUR", new(0))).Error.Should().Be(CommandError.Invalid);
        (await grain.SetExchangeRateAsync(member, "EUR", new(11.5m, 50.01m))).Error.Should().Be(CommandError.Invalid);
        (await grain.SetExchangeRateAsync(member, "EUR", new(11.5m))).Snapshot!.ExchangeRates.Should().Equal(new Dictionary<string, ExchangeRateSnapshot> { ["EUR"] = new(11.5m, 0) });
        var updated = (await grain.SetExchangeRateAsync(member, "EUR", new(11.25m, 2.5m))).Snapshot!;
        (await grain.SetExchangeRateAsync(member, "EUR", new(11.25m, 2.5m))).Snapshot!.Version.Should().Be(updated.Version);
        await grain.SetExchangeRateAsync(member, "USD", new(10));

        await _cluster.DeactivateAsync(grain);
        (await grain.GetSnapshotAsync(owner)).Snapshot!.ExchangeRates.Should().BeEquivalentTo(
            new Dictionary<string, ExchangeRateSnapshot> { ["EUR"] = new(11.25m, 2.5m), ["USD"] = new(10, 0) });
        (await grain.SetExchangeRateAsync(member, "USD", new(null))).Snapshot!.ExchangeRates["USD"].Should().Be(new ExchangeRateSnapshot(null, 0));
        (await grain.RemoveCurrencyAsync(member, "USD")).Snapshot!.ExchangeRates.Keys.Should().Equal("EUR");

        (await grain.SetBaseCurrencyAsync(member, "EUR")).Error.Should().Be(CommandError.Forbidden);
        (await grain.SetBaseCurrencyAsync(owner, "XXX")).Error.Should().Be(CommandError.Invalid);
        var rebased = (await grain.SetBaseCurrencyAsync(owner, "EUR")).Snapshot!;

        // The old base stays available without a rate.
        var unrated = new Dictionary<string, ExchangeRateSnapshot> { ["SEK"] = new(null, 0) };
        rebased.BaseCurrency.Should().Be("EUR");
        rebased.ExchangeRates.Should().BeEquivalentTo(unrated);
        await _cluster.DeactivateAsync(grain);
        (await grain.GetSnapshotAsync(owner)).Snapshot!.ExchangeRates.Should().BeEquivalentTo(unrated);

        await grain.SetTabsAsync(owner, new SetTabsRequest(true, false));
        (await grain.SetExchangeRateAsync(owner, "SEK", new(0.09m))).Error.Should().Be(CommandError.Forbidden);
    }

    [Test]
    public async Task Leave_RemovesMemberAndVotes_UnlessOwnerOrInAnExpense()
    {
        var owner = await AddUserAsync("Owner");
        var member = await AddUserAsync("Member");
        var payer = await AddUserAsync("Payer");
        var grain = await CreateAsync(owner);
        await grain.JoinAsync(member);
        await grain.JoinAsync(payer);
        var option = await AddOptionAsync(grain, member, 18);
        await grain.SetVoteAsync(member, option, VoteAnswer.Yes);
        await grain.AddExpenseAsync(payer, Dinner(payer, payer, owner));

        (await grain.LeaveAsync(owner)).Error.Should().Be(CommandError.Invalid);
        (await grain.LeaveAsync(payer)).Error.Should().Be(CommandError.Invalid);
        var left = (await grain.LeaveAsync(member)).Snapshot!;

        left.FindMember(member).Should().BeNull();
        left.TimeOptions.Single().Votes.Should().BeEmpty();
        (await grain.LeaveAsync(member)).Error.Should().Be(CommandError.NotMember);

        await _cluster.DeactivateAsync(grain);
        var reloaded = (await grain.GetSnapshotAsync(owner)).Snapshot!;
        reloaded.FindMember(member).Should().BeNull();
        reloaded.TimeOptions.Single().Votes.Should().BeEmpty();
        (await grain.JoinAsync(member)).Snapshot!.FindMember(member).Should().NotBeNull();
    }

    private static UpdateArrangementDetailsRequest Details(string title)
    {
        return new UpdateArrangementDetailsRequest(title, Request.Description);
    }

    private static ExpenseRequest Dinner(Guid paidBy, params Guid[] participants)
    {
        return new ExpenseRequest(" Dinner ", 30m, "SEK", paidBy, participants, new DateOnly(2026, 6, 19), false);
    }

    private static AddTimeOptionRequest Timed(int hour)
    {
        return new AddTimeOptionRequest(Evening.AddHours(hour), Evening.AddHours(hour + 3), false);
    }

    private static async Task<long> AddOptionAsync(IArrangementGrain grain, Guid userId, int hour)
    {
        var result = await grain.AddTimeOptionAsync(userId, Timed(hour));
        return result.Snapshot!.TimeOptions.Single(o => o.DateBegin == Evening.AddHours(hour)).Id;
    }

    private IArrangementGrain NewGrain()
    {
        return _cluster.Client.GetGrain<IArrangementGrain>(Guid.NewGuid());
    }

    private async Task<IArrangementGrain> CreateAsync(Guid owner)
    {
        var grain = NewGrain();
        (await grain.CreateAsync(owner, Request)).IsSuccess.Should().BeTrue();
        return grain;
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
}
