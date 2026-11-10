namespace CurrencyRates.Identity.Api.Contracts;

// Classes intentionally avoid record-generated ToString exposing passwords.
// Normalization and business validation belong to the Identity use cases.
public sealed class RegisterRequest
{
    public required string Name { get; init; }
    public required string Password { get; init; }
}

public sealed class LoginRequest
{
    public required string Name { get; init; }
    public required string Password { get; init; }
}
