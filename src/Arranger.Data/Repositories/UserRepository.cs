using System;
using Faemiyah.Arranger.Data.Entities;
using Microsoft.Extensions.Logging;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Repositories;

namespace Faemiyah.Arranger.Data.Repositories;

/// <summary>
/// Repository for <see cref="User"/> entities.
/// </summary>
public class UserRepository : RepositoryBaseWithUuid<ArrangerContext, long, User>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="databaseContextProvider">The database context provider.</param>
    /// <param name="timeProvider">The source of the current time, or <b>null</b> for the system clock.</param>
    public UserRepository(
        ILogger<UserRepository> logger,
        IDatabaseContextProvider<ArrangerContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }
}
