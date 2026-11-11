using CurrencyRates.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CurrencyRates.Identity.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void ModelMatchesMigrationAndNeverReferencesOtherSchemas()
    {
        using var context = new IdentityDesignTimeFactory().CreateDbContext([]);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.All(context.Model.GetEntityTypes(), entity =>
        {
            Assert.Equal("identity", entity.GetSchema());
            Assert.All(entity.GetForeignKeys(), fk => Assert.Equal("identity", fk.PrincipalEntityType.GetSchema()));
        });
    }
}
