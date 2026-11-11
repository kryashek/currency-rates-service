using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CurrencyRates.Finance.Infrastructure.Persistence;

// Design-time tooling only. No real credentials are needed to generate migrations.
public sealed class FinanceDesignTimeFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>();
        FinancePersistence.Configure(options, "Host=localhost;Database=currency_rates_design;Username=design_only");
        return new FinanceDbContext(options.Options);
    }
}
