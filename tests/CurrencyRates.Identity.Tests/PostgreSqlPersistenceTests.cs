using CurrencyRates.Identity.Domain.Entities;
using CurrencyRates.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CurrencyRates.Identity.Tests;

public sealed class PostgreSqlPersistenceTests
{
    private static NpgsqlConnection Connect() => new(Environment.GetEnvironmentVariable("CRS_TEST_IDENTITY"));

    [PostgreSqlFact]
    public async Task RuntimeCanPersistUserAndSessionWithUtcTimes()
    {
        await using var connection = Connect();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        IdentityPersistence.Configure(options, connection);
        await using var db = new IdentityDbContext(options.Options);
        await db.Database.UseTransactionAsync(transaction);
        var user = new User { Id = Guid.NewGuid(), Name = "Alice_1", NormalizedName = "ALICE_1", PasswordHash = "test-hash" };
        var session = new Session { Id = Guid.NewGuid(), UserId = user.Id, CreatedAt = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero), ExpiresAt = new DateTimeOffset(2026, 11, 10, 1, 0, 0, TimeSpan.Zero) };
        db.Users.Add(user);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.Sessions.SingleAsync(x => x.Id == session.Id);
        Assert.Equal(session.ExpiresAt, stored.ExpiresAt);
        Assert.Null(stored.RevokedAt);
        user = await db.Users.SingleAsync(x => x.Id == user.Id);
        user.PasswordHash = "updated-test-hash";
        await db.SaveChangesAsync();
        db.Sessions.Remove(stored);
        await db.SaveChangesAsync();
        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }

    [PostgreSqlTheory]
    [InlineData("INSERT INTO identity.\"user\" VALUES (gen_random_uuid(), 'ab', 'AB', 'hash')", "23514")]
    [InlineData("INSERT INTO identity.\"user\" VALUES (gen_random_uuid(), 'Alice', 'alice', 'hash')", "23514")]
    [InlineData("INSERT INTO identity.\"user\" VALUES ('00000000-0000-0000-0000-000000000000', 'Alice', 'ALICE', 'hash')", "23514")]
    [InlineData("INSERT INTO identity.\"user\" VALUES (gen_random_uuid(), 'Alice', 'ALICE', '')", "23514")]
    [InlineData("INSERT INTO identity.\"user\" VALUES (gen_random_uuid(), 'Alice', 'ALICE', 'hash'), (gen_random_uuid(), 'alice', 'ALICE', 'hash')", "23505")]
    [InlineData("INSERT INTO identity.session VALUES (gen_random_uuid(), gen_random_uuid(), now(), now() + interval '1 hour', NULL)", "23503")]
    [InlineData("INSERT INTO identity.session VALUES (gen_random_uuid(), gen_random_uuid(), now(), now(), NULL)", "23514")]
    [InlineData("SELECT * FROM finance.currency", "42501")]
    [InlineData("SELECT * FROM identity.\"__EFMigrationsHistory\"", "42501")]
    [InlineData("INSERT INTO identity.\"__EFMigrationsHistory\" VALUES ('fake','8.0.34')", "42501")]
    [InlineData("CREATE TABLE identity.forbidden(id int)", "42501")]
    [InlineData("CREATE TABLE public.forbidden(id int)", "42501")]
    [InlineData("CREATE SCHEMA forbidden", "42501")]
    [InlineData("CREATE TEMP TABLE forbidden(id int)", "42501")]
    [InlineData("ALTER TABLE identity.\"user\" ADD COLUMN forbidden int", "42501")]
    [InlineData("DROP TABLE identity.session", "42501")]
    [InlineData("TRUNCATE identity.session", "42501")]
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
}
