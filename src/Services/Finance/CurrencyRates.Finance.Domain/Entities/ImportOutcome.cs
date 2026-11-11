namespace CurrencyRates.Finance.Domain.Entities;

public enum ImportOutcome
{
    Applied = 1,
    Unchanged = 2,
    IgnoredOlder = 3
}
