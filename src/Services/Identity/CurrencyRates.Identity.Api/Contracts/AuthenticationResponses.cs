using System.Text.Json.Serialization;

namespace CurrencyRates.Identity.Api.Contracts;

public sealed record RegisterResponse(Guid UserId, string Name);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    [property: JsonConverter(typeof(UtcDateTimeOffsetConverter))] DateTimeOffset ExpiresAt,
    Guid UserId);
