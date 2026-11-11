namespace CurrencyRates.Finance.Domain.Entities;

public sealed class Favorite
{
    public Guid UserId { get; set; }
    public string CurrencyId { get; set; } = "";
}
