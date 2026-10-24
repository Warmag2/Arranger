using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Options;
using Faemiyah.Repository.Providers;

namespace Faemiyah.Arranger.Data;

/// <summary>
/// Supplies <see cref="ArrangerContext"/> instances, creating and migrating the database on first use.
/// </summary>
public class ArrangerContextProvider : PgSqlDatabaseContextProvider<ArrangerContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArrangerContextProvider"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="sqlOptions">The database options.</param>
    /// <param name="connectionStringAccessor">The connection string accessor.</param>
    // The base constructor dictates the logger type (S6672).
#pragma warning disable S6672
    public ArrangerContextProvider(
        ILogger<PgSqlDatabaseContextProvider<ArrangerContext>> logger,
        IOptions<SqlOptions> sqlOptions,
        IConnectionStringAccessor connectionStringAccessor)
#pragma warning restore S6672
        : base(logger, sqlOptions, connectionStringAccessor)
    {
    }

    /// <inheritdoc />
    protected override ArrangerContext GetContextInstance(DbContextOptions<ArrangerContext> dbContextOptions)
    {
        return new ArrangerContext(dbContextOptions);
    }

    /// <inheritdoc />
    protected override async Task OnDatabaseProvisionedAsync(
        ArrangerContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
