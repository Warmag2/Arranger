using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Faemiyah.Repository.Interfaces;

namespace Faemiyah.Arranger.Data.Queries;

/// <summary>
/// Read-only arrangement queries that span arrangements, and so bypass the grains.
/// </summary>
public class ArrangementQueries
{
    private readonly IDatabaseContextProvider<ArrangerContext> _contextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrangementQueries"/> class.
    /// </summary>
    /// <param name="contextProvider">The context provider.</param>
    public ArrangementQueries(IDatabaseContextProvider<ArrangerContext> contextProvider)
    {
        _contextProvider = contextProvider;
    }

    /// <summary>
    /// Get the arrangements a user is a member of, newest first.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The arrangements.</returns>
    public async Task<IReadOnlyList<ArrangementSummaryRow>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await context.ArrangementMembers
            .AsNoTracking()
            .Where(m => m.User!.UniqueId == userId)
            .OrderByDescending(m => m.Arrangement!.CreatedAt)
            .Select(m => new ArrangementSummaryRow(
                m.Arrangement!.UniqueId,
                m.Arrangement.Title,
                m.Arrangement.CreatedAt,
                m.Arrangement.Members.Count,
                m.Arrangement.OwnerUserId == m.UserId,
                m.IsAdmin))
            .ToListAsync(cancellationToken);
    }
}
