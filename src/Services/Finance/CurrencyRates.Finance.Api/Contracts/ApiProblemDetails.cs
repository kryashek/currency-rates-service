using System.Text.Json.Serialization;

namespace CurrencyRates.Finance.Api.Contracts;

// Transport shape only; exception mapping is implemented with the API handlers.
public sealed class ApiProblemDetails
{
    public string Type { get; init; } = "about:blank";
    public required string Title { get; init; }
    public required int Status { get; init; }
    public required string Detail { get; init; }
    public required string Instance { get; init; }
    public required string Code { get; init; }
    public required string TraceId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
