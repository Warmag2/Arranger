using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RepositoryPrototype.Interfaces;

namespace Faemiyah.Arranger.Data.Queries;

/// <summary>
/// Read-only user lookups that load only the columns needed.
/// </summary>
public class UserQueries
{
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
            .Select(u => new UserProfile(u.Id, u.UniqueId, u.UserName, u.DefaultCurrency))
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
            .Select(u => new UserProfile(u.Id, u.UniqueId, u.UserName, u.DefaultCurrency))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Whether a user has a profile picture.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><b>true</b> when the user has a picture.</returns>
    public async Task<bool> HasPictureAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await context.UserPictures.AnyAsync(p => p.User!.UniqueId == userId, cancellationToken);
    }

    /// <summary>
    /// Get a user's picture.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The picture data, or <b>null</b> when the user does not exist.</returns>
    public async Task<UserPictureData?> GetPictureAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.UniqueId == userId)
            .Select(u => new { u.Id, u.UserName })
            .SingleOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            return null;
        }

        var picture = await context.UserPictures
            .AsNoTracking()
            .Where(p => p.UserId == user.Id)
            .Select(p => new { p.Data, p.ContentType, p.ConcurrencyToken })
            .SingleOrDefaultAsync(cancellationToken);

        return new UserPictureData(user.UserName, picture?.Data, picture?.ContentType, picture?.ConcurrencyToken);
    }
}
