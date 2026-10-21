using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Faemiyah.Arranger.Data;

/// <summary>
/// Creates the context for the EF Core tools, so migrations can be added without starting the server.
/// </summary>
/// <remarks>
/// Adding a migration never connects, so the connection string only has to be well formed.
/// </remarks>
public class ArrangerContextDesignTimeFactory : IDesignTimeDbContextFactory<ArrangerContext>
{
    /// <inheritdoc />
    public ArrangerContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ArrangerContext>()
            .UseNpgsql("Host=localhost;Database=arranger")
            .Options;

        return new ArrangerContext(options);
    }
}
