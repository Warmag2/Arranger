using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Models;
using Faemiyah.Repository.Repositories;

namespace Faemiyah.Arranger.Data.Repositories;

/// <summary>
/// Repository for <see cref="Arrangement"/> entities. Includes load the members, the time options with their votes,
/// and the expenses with their participations.
/// </summary>
public class ArrangementRepository : RepositoryBaseWithUuid<ArrangerContext, long, Arrangement>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArrangementRepository"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="databaseContextProvider">The database context provider.</param>
    /// <param name="timeProvider">The source of the current time, or <b>null</b> for the system clock.</param>
    public ArrangementRepository(
        ILogger<ArrangementRepository> logger,
        IDatabaseContextProvider<ArrangerContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }

    /// <summary>
    /// Change the base currency in one save. The rates were relative to the old base, so they are cleared; the new base
    /// is no longer another currency, and the old base becomes one.
    /// </summary>
    /// <param name="arrangement">The arrangement with the new base currency, its ID and revision.</param>
    /// <param name="oldBaseCurrency">The previous base currency.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Updated"/> with the arrangement and its currencies, <see cref="OperationStatus.NotFound"/>,
    /// or <see cref="OperationStatus.Conflict"/> when the stored revision differs.
    /// </returns>
    public async Task<RepositoryResult<Arrangement>> ChangeBaseCurrencyAsync(Arrangement arrangement, string oldBaseCurrency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arrangement);
        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        var stored = await dbContext.Arrangements
            .Include(a => a.ExchangeRates)
            .FirstOrDefaultAsync(a => a.Id == arrangement.Id, cancellationToken);
        if (stored == null)
        {
            return RepositoryResult<Arrangement>.Failure(OperationStatus.NotFound);
        }

        // Removing a rate from the collection deletes it, since it cannot exist without its arrangement.
        stored.ExchangeRates.RemoveAll(r => r.Currency == arrangement.BaseCurrency);
        stored.ExchangeRates.ForEach(r => r.Rate = null);
        if (!stored.ExchangeRates.Exists(r => r.Currency == oldBaseCurrency))
        {
            stored.ExchangeRates.Add(new ExchangeRate { Currency = oldBaseCurrency });
        }

        return await TryUpdateWithContext(dbContext, stored, arrangement, cancellationToken);
    }

    /// <inheritdoc />
    protected override IQueryable<Arrangement> ProcessIncludes(DbSet<Arrangement> dbSet)
    {
        // Split, so that members, options, votes, expenses and rates are not multiplied into one result set.
        return dbSet
            .Include(a => a.Members)
            .Include(a => a.TimeOptions)
            .ThenInclude(o => o.Votes)
            .Include(a => a.Expenses)
            .ThenInclude(e => e.Participations)
            .Include(a => a.ExchangeRates)
            .AsSplitQuery();
    }
}
