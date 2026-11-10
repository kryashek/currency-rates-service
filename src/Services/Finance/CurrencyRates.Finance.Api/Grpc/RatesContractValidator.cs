using System.Globalization;
using System.Text.RegularExpressions;
using CurrencyRates.Contracts.Finance.V1;
using Google.Protobuf.WellKnownTypes;

namespace CurrencyRates.Finance.Api.Grpc;

// Payload validation only. CAS, deduplication and transactions are implemented in Finance use cases.
public static class RatesContractValidator
{
    public const int MaxRates = 1000;
    public const int MaxMessageBytes = 1024 * 1024;

    public static void Validate(ImportRatesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Round-trip comparison rejects whitespace and legacy forms accepted by TryParseExact.
        Require(Guid.TryParseExact(request.BatchId, "D", out var id) && id != Guid.Empty &&
            string.Equals(request.BatchId, id.ToString("D"), StringComparison.OrdinalIgnoreCase),
            nameof(request.BatchId), "A non-empty UUID in D format is required.");
        RequireDate(request.SourceDate, nameof(request.SourceDate));
        RequireTimestamp(request.FetchedAt, nameof(request.FetchedAt));
        Require(request.Rates.Count is >= 1 and <= MaxRates, nameof(request.Rates),
            "A complete snapshot must contain 1..1000 currencies.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rate in request.Rates)
        {
            Require(rate.CurrencyId.Length is >= 2 and <= 32 && rate.CurrencyId[0] == 'R' &&
                rate.CurrencyId.Skip(1).All(c => c is >= '0' and <= '9'), nameof(rate.CurrencyId),
                "A CBR ID starting with R followed by ASCII digits is required.");
            Require(rate.Code.Length == 3 && rate.Code.All(c => c is >= 'A' and <= 'Z'),
                nameof(rate.Code), "Three uppercase ASCII letters are required.");
            Require(rate.Name.Length <= 200 && !string.IsNullOrWhiteSpace(rate.Name),
                nameof(rate.Name), "A non-blank currency name of at most 200 characters is required.");
            Require(rate.Rate.Length is >= 1 and <= 21 &&
                Matches(rate.Rate, "^[0-9]{1,12}([.][0-9]{1,8})?$") &&
                decimal.TryParse(rate.Rate, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                    out var value) && value > 0 && value < 1_000_000_000_000m,
                nameof(rate.Rate), "A positive invariant decimal with at most 8 fractional digits is required.");
            Require(ids.Add(rate.CurrencyId), nameof(request.Rates), "Duplicate currency ID.");
            Require(codes.Add(rate.Code), nameof(request.Rates), "Duplicate currency code.");
        }

        Require(request.CalculateSize() <= MaxMessageBytes, nameof(request), "The message exceeds 1 MiB.");
    }

    public static void Validate(ImportState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SnapshotVersion == 0)
        {
            Require(state.SourceDate.Length == 0 && state.LoadedAt is null, nameof(state),
                "An initial state must have no source date or loaded timestamp.");
            return;
        }

        RequireDate(state.SourceDate, nameof(state.SourceDate));
        RequireTimestamp(state.LoadedAt, nameof(state.LoadedAt));
    }

    public static void Validate(ImportRatesResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Require(response.Outcome is ImportOutcome.Applied or ImportOutcome.Unchanged or ImportOutcome.IgnoredOlder,
            nameof(response.Outcome), "A successful import outcome is required.");
        Require(response.State is not null, nameof(response.State), "Import state is required.");
        Validate(response.State!);
    }

    private static bool Matches(string value, string pattern) =>
        Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static void RequireDate(string value, string field) =>
        Require(DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _), field, "A valid date in YYYY-MM-DD format is required.");

    private static void RequireTimestamp(Timestamp? value, string field)
    {
        Require(value is not null, field, "A timestamp is required.");
        // Protobuf's specified range: 0001-01-01..9999-12-31, normalized nanos.
        Require(value!.Seconds is >= -62_135_596_800 and <= 253_402_300_799 &&
            value.Nanos is >= 0 and <= 999_999_999, field, "A normalized protobuf timestamp is required.");
    }

    private static void Require(bool condition, string field, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message, field);
        }
    }
}
