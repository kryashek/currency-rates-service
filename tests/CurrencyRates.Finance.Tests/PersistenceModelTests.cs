using CurrencyRates.Finance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CurrencyRates.Finance.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void ModelMatchesMigrationAndNeverReferencesOtherSchemas()
    {
        using var context = new FinanceDesignTimeFactory().CreateDbContext([]);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.All(context.Model.GetEntityTypes(), entity =>
        {
            Assert.Equal("finance", entity.GetSchema());
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal("finance", fk.PrincipalEntityType.GetSchema()));
        });
    }
}
