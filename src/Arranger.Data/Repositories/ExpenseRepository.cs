using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepositoryPrototype.Interfaces;
using RepositoryPrototype.Models;
using RepositoryPrototype.Repositories;

namespace Faemiyah.Arranger.Data.Repositories;

/// <summary>
/// Repository for <see cref="Expense"/> entities. Adding an expense adds its participations too.
/// </summary>
public class ExpenseRepository : RepositoryBase<ArrangerContext, long, Expense>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpenseRepository"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="databaseContextProvider">The database context provider.</param>
    /// <param name="timeProvider">The source of the current time, or <b>null</b> for the system clock.</param>
    public ExpenseRepository(
        ILogger<ExpenseRepository> logger,
        IDatabaseContextProvider<ArrangerContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }

    /// <summary>
    /// Replace an expense's columns and participants in one save. Participations of the same user are kept.
    /// </summary>
    /// <param name="expense">The expense, with its ID, revision and the wanted participations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Updated"/> with the expense and its participations, <see cref="OperationStatus.NotFound"/>,
    /// or <see cref="OperationStatus.Conflict"/> when the stored revision differs.
    /// </returns>
    public async Task<RepositoryResult<Expense>> ReplaceAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);
        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        var stored = await dbContext.Expenses
            .Include(e => e.Participations)
            .FirstOrDefaultAsync(e => e.Id == expense.Id, cancellationToken);
        if (stored == null)
        {
            return RepositoryResult<Expense>.Failure(OperationStatus.NotFound);
        }

        // Removing a participation from the collection deletes it, since it cannot exist without its expense.
        var wanted = expense.Participations.Select(p => p.UserId).ToHashSet();
        stored.Participations.RemoveAll(p => !wanted.Contains(p.UserId));
        wanted.ExceptWith(stored.Participations.Select(p => p.UserId));
        stored.Participations.AddRange(wanted.Select(userId => new ExpenseParticipation { UserId = userId }));
        return await TryUpdateWithContext(dbContext, stored, expense, cancellationToken);
    }

    /// <inheritdoc />
    protected override IQueryable<Expense> ProcessIncludes(DbSet<Expense> dbSet)
    {
        return dbSet.Include(e => e.Participations);
    }
}
