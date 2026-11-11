using System.Data;
using System.Data.Common;
using CurrencyRates.Finance.Infrastructure.Persistence;
using CurrencyRates.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CurrencyRates.DbMigrator;

public static class MigrationRunner
{
    // Stable across versions and processes; advisory locks are scoped to this database.
    public const long LockKey = 0x4352534D49475201;

    public static async Task<int> RunAsync(string? connectionString, CancellationToken cancellationToken = default)
    {
        NpgsqlConnectionStringBuilder settings;
        try
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException();
            settings = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Database) ||
                string.IsNullOrWhiteSpace(settings.Username))
                throw new ArgumentException();
            settings.Pooling = false;
            settings.Multiplexing = false;
            settings.IncludeErrorDetail = false;
            settings.ApplicationName = "CurrencyRates.DbMigrator";
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine("Invalid configuration. Set ConnectionStrings__Migrator with an explicit host, database and deployment user.");
            return 2;
        }

        try
        {
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using (var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection))
            {
                command.Parameters.AddWithValue("key", LockKey);
                if (await command.ExecuteScalarAsync(cancellationToken) is not true)
                {
                    Console.Error.WriteLine("Another DbMigrator is running. Retry after it finishes.");
                    return 3;
                }
            }
            Console.WriteLine("Migration lock acquired.");

            var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>();
            IdentityPersistence.Configure(identityOptions, connection);
            identityOptions.AddInterceptors(new RejectReconnectionInterceptor());
            await using (var identity = new IdentityDbContext(identityOptions.Options))
                await ApplyAsync(identity, "Identity", cancellationToken);

            var financeOptions = new DbContextOptionsBuilder<FinanceDbContext>();
            FinancePersistence.Configure(financeOptions, connection);
            financeOptions.AddInterceptors(new RejectReconnectionInterceptor());
            await using (var finance = new FinanceDbContext(financeOptions.Options))
                await ApplyAsync(finance, "Finance", cancellationToken);

            // Disposing the non-pooled physical connection releases the session lock,
            // including on cancellation/failure. It must not be released between contexts.
            Console.WriteLine("Database migrations completed successfully.");
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Exception messages/SQL/connection strings can contain credentials or data.
            Console.Error.WriteLine($"Database migration failed ({exception.GetType().Name}, SQLSTATE={(exception as PostgresException)?.SqlState ?? "none"}). Correct the deployment configuration or database state, then retry.");
            return 1;
        }
    }

    private static async Task ApplyAsync(DbContext context, string service, CancellationToken cancellationToken)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
        Console.WriteLine($"{service}: {pending} pending migration(s).");
        await context.Database.MigrateAsync(cancellationToken);
        Console.WriteLine($"{service}: up to date.");
    }

    private sealed class RejectReconnectionInterceptor : DbConnectionInterceptor
    {
        private static InterceptionResult CheckOpenConnection(DbConnection connection)
        {
            // Npgsql 8 invokes ConnectionOpening even for its database-exists probe.
            // The locked connection is already open, so existence is established;
            // suppress the probe rather than letting it clone/open another session.
            if (connection.State == ConnectionState.Open)
                return InterceptionResult.Suppress();
            throw new InvalidOperationException("The migration lock connection cannot be reopened.");
        }
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result) => CheckOpenConnection(connection);

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CheckOpenConnection(connection));
    }
}
