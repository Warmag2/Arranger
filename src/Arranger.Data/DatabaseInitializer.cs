using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Faemiyah.Repository.Interfaces;

namespace Faemiyah.Arranger.Data;

/// <summary>
/// Creates and migrates the database at startup.
/// </summary>
/// <remarks>
/// The provider migrates lazily on its first context anyway. Asking for one here, before the server accepts requests,
/// makes a broken connection or migration fail the startup instead of the first request. The provider migrates once
/// per process, so this adds no second migration run.
/// </remarks>
public class DatabaseInitializer : IHostedService
{
    private readonly IDatabaseContextProvider<ArrangerContext> _contextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseInitializer"/> class.
    /// </summary>
    /// <param name="contextProvider">The context provider.</param>
    public DatabaseInitializer(IDatabaseContextProvider<ArrangerContext> contextProvider)
    {
        _contextProvider = contextProvider;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _contextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
