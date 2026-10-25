using System;
using Faemiyah.Arranger.Data.Entities;
using Microsoft.Extensions.Logging;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Repositories;

namespace Faemiyah.Arranger.Data.Repositories;

/// <summary>
/// Repository for <see cref="ExchangeRate"/> entities.
/// </summary>
public class ExchangeRateRepository : RepositoryBase<ArrangerContext, long, ExchangeRate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExchangeRateRepository"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="databaseContextProvider">The database context provider.</param>
    /// <param name="timeProvider">The source of the current time, or <b>null</b> for the system clock.</param>
    public ExchangeRateRepository(
        ILogger<ExchangeRateRepository> logger,
        IDatabaseContextProvider<ArrangerContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }
}
