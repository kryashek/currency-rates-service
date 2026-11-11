using CurrencyRates.Finance.Domain.Entities;
using CurrencyRates.Finance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CurrencyRates.Finance.Tests;

public sealed class PostgreSqlPersistenceTests
{
    private static NpgsqlConnection Connect() => new(Environment.GetEnvironmentVariable("CRS_TEST_FINANCE"));

    [PostgreSqlFact]
    public async Task RuntimeRoundTripsDecimalAndFullUint64RangeWithoutIdentityRow()
    {
        await using var connection = Connect();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var options = new DbContextOptionsBuilder<FinanceDbContext>();
        FinancePersistence.Configure(options, connection);
        await using var db = new FinanceDbContext(options.Options);
        await db.Database.UseTransactionAsync(transaction);
        var currency = new Currency { Id = "R01235", Code = "USD", Name = "Dollar", IsAvailable = true, Rate = 999999999999.99999999m };
        var favorite = new Favorite { UserId = Guid.NewGuid(), CurrencyId = currency.Id };
        db.Currencies.Add(currency);
        db.Favorites.Add(favorite);
        var state = await db.ImportStates.SingleAsync();
        Assert.Equal(0UL, state.SnapshotVersion);
        Assert.Null(state.SourceDate);
        Assert.Null(state.LoadedAt);
        state.SnapshotVersion = ulong.MaxValue;
        state.SourceDate = new DateOnly(2026, 11, 10);
        state.LoadedAt = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
        var batch = new ImportBatch { BatchId = Guid.NewGuid(), RequestHash = new byte[32], ExpectedSnapshotVersion = ulong.MaxValue - 1, SourceDate = state.SourceDate.Value, FetchedAt = state.LoadedAt.Value, Outcome = ImportOutcome.Applied, SnapshotVersion = ulong.MaxValue, StateSourceDate = state.SourceDate.Value, StateLoadedAt = state.LoadedAt.Value };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(currency.Rate, (await db.Currencies.SingleAsync()).Rate);
        Assert.Equal(ulong.MaxValue, (await db.ImportStates.SingleAsync()).SnapshotVersion);
        Assert.Equal(ulong.MaxValue, (await db.ImportBatches.SingleAsync()).SnapshotVersion);
        db.Favorites.Remove(await db.Favorites.SingleAsync());
        await db.SaveChangesAsync();
        currency = await db.Currencies.SingleAsync();
        currency.IsAvailable = false;
        currency.Rate = null;
        await db.SaveChangesAsync();
        db.Currencies.Remove(currency);
        await db.SaveChangesAsync();
    }

    [PostgreSqlTheory]
    [InlineData("INSERT INTO finance.currency VALUES ('R1','USD','Dollar',true,0)", "23514")]
    [InlineData("INSERT INTO finance.currency VALUES ('R1','USD','Dollar',true,NULL)", "23514")]
    [InlineData("INSERT INTO finance.currency VALUES ('R1','USD','Dollar',false,1)", "23514")]
    [InlineData("INSERT INTO finance.currency VALUES ('R1','USD','Dollar',true,'NaN')", "23514")]
    [InlineData("INSERT INTO finance.currency VALUES ('R1','usd','Dollar',true,1)", "23514")]
    [InlineData("INSERT INTO finance.currency VALUES ('R1','USD','   ',true,1)", "23514")]
    [InlineData("INSERT INTO finance.currency VALUES ('R1','USD','Dollar',true,1),('R2','USD','Dollar',true,2)", "23505")]
    [InlineData("INSERT INTO finance.favorite VALUES (gen_random_uuid(),'R404')", "23503")]
    [InlineData("UPDATE finance.import_state SET snapshot_version=18446744073709551616", "23514")]
    [InlineData("UPDATE finance.import_state SET snapshot_version=1", "23514")]
    [InlineData("UPDATE finance.import_state SET id=2", "23514")]
    [InlineData("SELECT * FROM identity.\"user\"", "42501")]
    [InlineData("SELECT * FROM finance.\"__EFMigrationsHistory\"", "42501")]
    [InlineData("INSERT INTO finance.\"__EFMigrationsHistory\" VALUES ('fake','8.0.34')", "42501")]
    [InlineData("CREATE TABLE finance.forbidden(id int)", "42501")]
    [InlineData("CREATE TABLE public.forbidden(id int)", "42501")]
    [InlineData("CREATE SCHEMA forbidden", "42501")]
    [InlineData("CREATE TEMP TABLE forbidden(id int)", "42501")]
    [InlineData("ALTER TABLE finance.currency ADD COLUMN forbidden int", "42501")]
    [InlineData("DROP TABLE finance.favorite", "42501")]
    [InlineData("TRUNCATE finance.currency", "42501")]
    [InlineData("SET ROLE rates_migrator", "42501")]
    public async Task ConstraintsAndLeastPrivilegeAreEnforced(string sql, string expectedState)
    {
        await using var connection = Connect();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(expectedState, error.SqlState);
    }


    [PostgreSqlTheory]
    [InlineData("\u00a0")]
    [InlineData("\u2003")]
    [InlineData("\u0085")]
    [InlineData("\u3000")]
    [InlineData("\t \r\n\u202f\u205f")]
    public async Task UnicodeWhitespaceOnlyNameIsRejected(string name)
    {
        await using var connection = Connect();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("INSERT INTO finance.currency VALUES ('R1','USD',@name,true,1)", connection, transaction);
        command.Parameters.AddWithValue("name", name);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("23514", error.SqlState);
    }

    [PostgreSqlFact]
    public async Task InactiveCodeCanBeReusedButFavoritePairAndLocalForeignKeyAreEnforced()
    {
        await using var connection = Connect();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var setup = new NpgsqlCommand("INSERT INTO finance.currency VALUES ('R1','USD','Old',false,NULL),('R2','USD','New',true,1); INSERT INTO finance.favorite VALUES ('11111111-1111-1111-1111-111111111111','R1')", connection, transaction);
        await setup.ExecuteNonQueryAsync();
        await using var duplicate = new NpgsqlCommand("INSERT INTO finance.favorite VALUES ('11111111-1111-1111-1111-111111111111','R1')", connection, transaction);
        var error = await Assert.ThrowsAsync<PostgresException>(() => duplicate.ExecuteNonQueryAsync());
        Assert.Equal("23505", error.SqlState);
    }
}
