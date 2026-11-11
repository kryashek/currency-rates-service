using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CurrencyRates.Identity.Infrastructure.Persistence;

// Design-time tooling only. No real credentials are needed to generate migrations.
public sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        IdentityPersistence.Configure(options, "Host=localhost;Database=currency_rates_design;Username=design_only");
        return new IdentityDbContext(options.Options);
    }
}
