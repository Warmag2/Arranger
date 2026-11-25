using System;
using System.Collections.Generic;
using Faemiyah.Arranger.Data.Entities;
using Faemiyah.Arranger.Data.Extensions;
using Faemiyah.Arranger.Server.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faemiyah.Arranger.IntegrationTests;

/// <summary>
/// Builds the services for tests of the account code, without the rest of the server.
/// </summary>
public static class TestServices
{
    /// <summary>
    /// Build the data layer on a new database in the shared container, and the account service on top of it.
    /// </summary>
    /// <param name="configure">Adds further services, or <b>null</b>.</param>
    /// <returns>The service provider. Dispose it after the test.</returns>
    public static ServiceProvider Create(Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = PostgreSqlFixture.BaseConnectionString,
                ["Sql:DefaultDbName"] = "arranger_" + Guid.NewGuid().ToString("N"),
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddArrangerData(configuration)
            .AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>()
            .AddSingleton<AccountService>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }
}
