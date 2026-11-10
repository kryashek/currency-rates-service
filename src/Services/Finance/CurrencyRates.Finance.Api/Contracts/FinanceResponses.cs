using System.Text.Json.Serialization;

namespace CurrencyRates.Finance.Api.Contracts;

public sealed record CurrencyResponse(string CurrencyId, string Code, string Name);

public sealed record FavoriteResponse(string CurrencyId, string Code, string Name, bool IsAvailable);

public sealed record CurrencyRateResponse(
    string CurrencyId,
    string Code,
    string Name,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] decimal? Rate,
    bool IsAvailable);

// SourceDate, LoadedAt and Items must be read from one database snapshot.
public sealed record RatesResponse(
    DateOnly SourceDate,
    [property: JsonConverter(typeof(UtcDateTimeOffsetConverter))] DateTimeOffset LoadedAt,
    IReadOnlyList<CurrencyRateResponse> Items);
