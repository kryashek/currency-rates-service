namespace CurrencyRates.Identity.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
