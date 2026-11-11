using Xunit;

namespace CurrencyRates.Identity.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CRS_TEST_IDENTITY")))
            Skip = "Run tests/Verify-Database.ps1 against an isolated PostgreSQL 16 cluster.";
    }
}

public sealed class PostgreSqlTheoryAttribute : TheoryAttribute
{
    public PostgreSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CRS_TEST_IDENTITY")))
            Skip = "Run tests/Verify-Database.ps1 against an isolated PostgreSQL 16 cluster.";
    }
}
