using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Data;
using Faemiyah.Arranger.Data.Entities;
using Faemiyah.Arranger.Data.Repositories;
using Faemiyah.Arranger.Server.Authentication;
using Faemiyah.Repository.Entities;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Faemiyah.Arranger.IntegrationTests;

[TestFixture]
public sealed class RepositoryTests
{
    private const long MissingId = long.MaxValue;
    private const string Password = "correct horse";

    private AccountService _accounts = null!;
    private ArrangementRepository _arrangements = null!;
    private IDatabaseContextProvider<ArrangerContext> _contexts = null!;
    private ExpenseRepository _expenses = null!;
    private ArrangementMemberRepository _members = null!;
    private ServiceProvider _services = null!;

    [SetUp]
    public void SetUp()
    {
        _services = TestServices.Create();
        _accounts = _services.GetRequiredService<AccountService>();
        _arrangements = _services.GetRequiredService<ArrangementRepository>();
        _contexts = _services.GetRequiredService<IDatabaseContextProvider<ArrangerContext>>();
        _expenses = _services.GetRequiredService<ExpenseRepository>();
        _members = _services.GetRequiredService<ArrangementMemberRepository>();
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        await _services.DisposeAsync();
    }

    [Test]
    public async Task ChangeBaseCurrency_MissingArrangement_IsNotFound()
    {
        var result = await _arrangements.ChangeBaseCurrencyAsync(new Arrangement { Id = MissingId, BaseCurrency = "SEK" }, "EUR");

        result.Status.Should().Be(OperationStatus.NotFound);
    }

    [Test]
    public async Task ChangeBaseCurrency_StaleRevision_IsConflictAndKeepsRates()
    {
        var arrangement = await SeedAsync();
        var oldBaseCurrency = arrangement.BaseCurrency;
        await WriteElsewhereAsync<Arrangement>(arrangement.Id);
        arrangement.BaseCurrency = "SEK";

        var result = await _arrangements.ChangeBaseCurrencyAsync(arrangement, oldBaseCurrency);

        result.Status.Should().Be(OperationStatus.Conflict);
        var stored = await ReloadAsync(arrangement);
        stored.BaseCurrency.Should().Be(oldBaseCurrency);
        stored.ExchangeRates.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Currency = "SEK", Rate = 11.5m });
    }

    [Test]
    public async Task DeleteWithVotes_MissingMember_IsNotFound()
    {
        var result = await _members.DeleteWithVotesAsync(new ArrangementMember { Id = MissingId });

        result.Status.Should().Be(OperationStatus.NotFound);
    }

    [Test]
    public async Task DeleteWithVotes_StaleRevision_IsConflictAndKeepsVotes()
    {
        var arrangement = await SeedAsync();
        var member = arrangement.Members.Single(m => !m.IsAdmin);
        await WriteElsewhereAsync<ArrangementMember>(member.Id);

        var result = await _members.DeleteWithVotesAsync(member);

        result.Status.Should().Be(OperationStatus.Conflict);
        var stored = await ReloadAsync(arrangement);
        stored.Members.Should().HaveCount(2);
        stored.TimeOptions.Single().Votes.Should().ContainSingle(v => v.UserId == member.UserId);
    }

    [Test]
    public async Task RemovePickedTimeOption_MissingOption_IsNotFound()
    {
        var arrangement = await SeedAsync();

        var result = await _arrangements.RemovePickedTimeOptionAsync(arrangement, MissingId);

        result.Status.Should().Be(OperationStatus.NotFound);
    }

    [Test]
    public async Task RemovePickedTimeOption_StaleRevision_IsConflictAndKeepsOption()
    {
        var arrangement = await SeedAsync();
        var option = arrangement.TimeOptions.Single();
        await WriteElsewhereAsync<Arrangement>(arrangement.Id);

        var result = await _arrangements.RemovePickedTimeOptionAsync(arrangement, option.Id);

        result.Status.Should().Be(OperationStatus.Conflict);
        (await ReloadAsync(arrangement)).TimeOptions.Should().ContainSingle(o => o.Id == option.Id).Which.Votes.Should().ContainSingle();
    }

    [Test]
    public async Task Replace_MissingExpense_IsNotFound()
    {
        var result = await _expenses.ReplaceAsync(new Expense { Id = MissingId });

        result.Status.Should().Be(OperationStatus.NotFound);
    }

    [Test]
    public async Task Replace_StaleRevision_IsConflictAndKeepsParticipants()
    {
        var arrangement = await SeedAsync();
        var expense = arrangement.Expenses.Single();
        await WriteElsewhereAsync<Expense>(expense.Id);
        expense.Description = "Edited";
        expense.Participations.RemoveAll(p => p.UserId != arrangement.OwnerUserId);

        var result = await _expenses.ReplaceAsync(expense);

        result.Status.Should().Be(OperationStatus.Conflict);
        var stored = (await ReloadAsync(arrangement)).Expenses.Single();
        stored.Description.Should().Be("Dinner");
        stored.Participations.Should().HaveCount(2);
    }

    private async Task<Arrangement> ReloadAsync(Arrangement arrangement)
    {
        return (await _arrangements.GetAsync(arrangement.UniqueId, processIncludes: true))!;
    }

    private async Task<Arrangement> SeedAsync()
    {
        // An admin owner, a second member who voted on the one time option, an expense shared by both, and a SEK
        // rate. Read back from the database, so every row carries its current revision.
        var owner = (await _accounts.RegisterAsync("Owner", Password, null)).User!;
        var member = (await _accounts.RegisterAsync("Member", Password, null)).User!;
        var now = DateTime.UtcNow;
        var arrangement = new Arrangement
        {
            Title = "Repository test",
            OwnerUserId = owner.Id,
            CreatedAt = now,
            Members =
            [
                new ArrangementMember { UserId = owner.Id, IsAdmin = true, JoinedAt = now },
                new ArrangementMember { UserId = member.Id, JoinedAt = now },
            ],
            TimeOptions =
            [
                new TimeOption
                {
                    DateBegin = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsWholeDay = true,
                    CreatedByUserId = owner.Id,
                    Votes = [new Vote { UserId = member.Id, Answer = VoteAnswer.Yes }],
                },
            ],
            Expenses =
            [
                new Expense
                {
                    Description = "Dinner",
                    Amount = 10m,
                    Currency = "SEK",
                    PaidByUserId = owner.Id,
                    Date = new DateOnly(2030, 1, 1),
                    Participations = [new ExpenseParticipation { UserId = owner.Id }, new ExpenseParticipation { UserId = member.Id }],
                },
            ],
            ExchangeRates = [new ExchangeRate { Currency = "SEK", Rate = 11.5m }],
        };

        (await _arrangements.AddAsync(arrangement)).IsSuccess.Should().BeTrue();
        return await ReloadAsync(arrangement);
    }

    private async Task WriteElsewhereAsync<TEntity>(long id)
        where TEntity : EntityBase<long>
    {
        // Any write gives the row a new revision, as if another writer had got there first.
        await using var dbContext = await _contexts.GetDbContextAsync();
        await dbContext.Set<TEntity>().Where(e => e.Id == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.UpdateTime, DateTime.UtcNow));
    }
}
