using System;
using Faemiyah.Arranger.Data.Entities;
using Faemiyah.Arranger.Data.Queries;
using Faemiyah.Arranger.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Faemiyah.Repository.Extensions;
using Faemiyah.Repository.Interfaces;

namespace Faemiyah.Arranger.Data.Extensions;

/// <summary>
/// Registration helpers for the Arranger data layer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The configuration section holding the Faemiyah.Repository <c>SqlOptions</c>.
    /// </summary>
    public const string SqlSectionName = "Sql";

    /// <summary>
    /// Registers the context provider, the repositories and the queries, and migrates the database at startup.
    /// </summary>
    /// <remarks>
    /// Requires <c>ConnectionStrings:Database</c> and <c>Sql:DefaultDbName</c>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddArrangerData(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddPgSqlRepositoryProvider<ArrangerContext, ArrangerContextProvider>(
            options => configuration.GetSection(SqlSectionName).Bind(options));

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IEntityRepositoryWithUuid<long, User>, UserRepository>();
        services.AddSingleton<IEntityRepository<long, UserPicture>, UserPictureRepository>();
        services.AddSingleton<ArrangementRepository>();
        services.AddSingleton<IEntityRepositoryWithUuid<long, Arrangement>>(sp => sp.GetRequiredService<ArrangementRepository>());
        services.AddSingleton<ArrangementMemberRepository>();
        services.AddSingleton<IEntityRepository<long, ArrangementMember>>(sp => sp.GetRequiredService<ArrangementMemberRepository>());
        services.AddSingleton<IEntityRepository<long, TimeOption>, TimeOptionRepository>();
        services.AddSingleton<IEntityRepository<long, Vote>, VoteRepository>();
        services.AddSingleton<ExpenseRepository>();
        services.AddSingleton<IEntityRepository<long, ExchangeRate>, ExchangeRateRepository>();
        services.AddSingleton<UserQueries>();
        services.AddSingleton<ArrangementQueries>();

        services.AddHostedService<DatabaseInitializer>();

        return services;
    }
}
