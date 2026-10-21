using System;
using Faemiyah.Arranger.Data.Entities;
using Microsoft.Extensions.Logging;
using RepositoryPrototype.Interfaces;
using RepositoryPrototype.Repositories;

namespace Faemiyah.Arranger.Data.Repositories;

/// <summary>
/// Repository for <see cref="Vote"/> entities.
/// </summary>
public class VoteRepository : RepositoryBase<ArrangerContext, long, Vote>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VoteRepository"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="databaseContextProvider">The database context provider.</param>
    /// <param name="timeProvider">The source of the current time, or <b>null</b> for the system clock.</param>
    public VoteRepository(
        ILogger<VoteRepository> logger,
        IDatabaseContextProvider<ArrangerContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }
}
