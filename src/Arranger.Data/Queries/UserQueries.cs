using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Faemiyah.Repository.Interfaces;

namespace Faemiyah.Arranger.Data.Queries;

/// <summary>
/// Read-only user lookups that load only the columns needed.
/// </summary>
public class UserQueries
{
    private static readonly Expression<Func<User, UserProfile>> ToProfile =
        u => new UserProfile(u.Id, u.UniqueId, u.UserName, u.DefaultCurrency, u.Picture != null ? u.Picture.ConcurrencyToken : null);

    private readonly IDatabaseContextProvider<ArrangerContext> _contextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserQueries"/> class.
    /// </summary>
    /// <param name="contextProvider">The context provider.</param>
    public UserQueries(IDatabaseContextProvider<ArrangerContext> contextProvider)
    {
        _contextProvider = contextProvider;
    }

    /// <summary>
    /// Get a user's profile picture.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The picture, or <b>null</b> when the user does not exist or has no picture.</returns>
    public async Task<UserPictureData?> GetPictureAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await context.UserPictures
            .AsNoTracking()
            .Where(p => p.User!.UniqueId == userId)
            .Select(p => new UserPictureData(p.Data, p.ContentType))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Get a user's profile by public ID.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profile, or <b>null</b> when the user does not exist.</returns>
    public async Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await context.Users
            .AsNoTracking()
            .Where(u => u.UniqueId == userId)
            .Select(ToProfile)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Get the profiles of several users by internal ID.
    /// </summary>
    /// <param name="ids">The internal user IDs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profiles of the users that exist.</returns>
    public async Task<IReadOnlyList<UserProfile>> GetProfilesAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return [];
        }

        await using var context = await _contextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await context.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(ToProfile)
            .ToListAsync(cancellationToken);
    }
}