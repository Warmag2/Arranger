using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepositoryPrototype.Entities;
using RepositoryPrototype.Interfaces;
using RepositoryPrototype.Models;
using RepositoryPrototype.Repositories;

namespace Faemiyah.Arranger.Data.Repositories;

/// <summary>
/// Repository for <see cref="ArrangementMember"/> entities.
/// </summary>
public class ArrangementMemberRepository : RepositoryBase<ArrangerContext, long, ArrangementMember>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArrangementMemberRepository"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="databaseContextProvider">The database context provider.</param>
    /// <param name="timeProvider">The source of the current time, or <b>null</b> for the system clock.</param>
    public ArrangementMemberRepository(
        ILogger<ArrangementMemberRepository> logger,
        IDatabaseContextProvider<ArrangerContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }

    /// <summary>
    /// Delete a membership and the member's votes in the arrangement in one save.
    /// </summary>
    /// <param name="member">The membership, with its ID and revision.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Deleted"/>, <see cref="OperationStatus.NotFound"/>,
    /// or <see cref="OperationStatus.Conflict"/> when the stored revision differs.
    /// </returns>
    public async Task<RepositoryResult<ArrangementMember>> DeleteWithVotesAsync(ArrangementMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);
        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        var stored = await dbContext.ArrangementMembers.FirstOrDefaultAsync(m => m.Id == member.Id, cancellationToken);
        if (stored == null)
        {
            return RepositoryResult<ArrangementMember>.Failure(OperationStatus.NotFound);
        }

        var votes = await dbContext.Votes
            .Where(v => v.UserId == member.UserId && v.TimeOption!.ArrangementId == member.ArrangementId)
            .ToListAsync(cancellationToken);
        dbContext.Votes.RemoveRange(votes);
        var entry = dbContext.ArrangementMembers.Remove(stored);
        entry.OriginalValues[nameof(EntityBase<>.ConcurrencyToken)] = member.ConcurrencyToken;
        return await SaveWithContextAsync(dbContext, OperationStatus.Deleted, stored, cancellationToken);
    }
}
