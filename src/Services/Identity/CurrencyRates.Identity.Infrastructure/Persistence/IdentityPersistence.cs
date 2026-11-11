using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CurrencyRates.Identity.Infrastructure.Persistence;

public static class IdentityPersistence
{
    public static IServiceCollection AddIdentityPersistence(this IServiceCollection services, string connectionString)
        => services.AddDbContext<IdentityDbContext>(options => Configure(options, connectionString));

    public static void Configure(DbContextOptionsBuilder options, string connectionString)
        => options.UseNpgsql(connectionString, ConfigureProvider);

    public static void Configure(DbContextOptionsBuilder options, DbConnection connection)
        => options.UseNpgsql(connection, ConfigureProvider);

    private static void ConfigureProvider(NpgsqlDbContextOptionsBuilder provider)
        => provider.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
}
