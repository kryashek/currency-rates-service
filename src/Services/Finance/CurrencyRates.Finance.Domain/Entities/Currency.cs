namespace CurrencyRates.Finance.Domain.Entities;

public sealed class Currency
{
    public string Id { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsAvailable { get; set; }
    public decimal? Rate { get; set; }
}
