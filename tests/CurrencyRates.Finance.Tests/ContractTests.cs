extern alias RatesClient;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CurrencyRates.Contracts.Finance.V1;
using CurrencyRates.Finance.Api.Contracts;
using CurrencyRates.Finance.Api.Grpc;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.OpenApi.Readers;
using Xunit;
using ClientRequest = RatesClient::CurrencyRates.Contracts.Finance.V1.ImportRatesRequest;
using ClientResponse = RatesClient::CurrencyRates.Contracts.Finance.V1.ImportRatesResponse;
using ClientRate = RatesClient::CurrencyRates.Contracts.Finance.V1.CurrencyRate;

namespace CurrencyRates.Finance.Tests;

public sealed class ContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static ImportRatesRequest ValidRequest() => new()
    {
        BatchId = "11111111-1111-4111-8111-111111111111", ExpectedSnapshotVersion = 7,
        SourceDate = "2026-11-10", FetchedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
        Rates = { new CurrencyRate { CurrencyId = "R01235", Code = "USD", Name = "Доллар США", Rate = "95.12340000" } }
    };

    [Fact]
    public void WorkerAndServerPreserveRatePrecisionAndExpectedVersion()
    {
        var client = new ClientRequest
        {
            BatchId = Guid.NewGuid().ToString("D"), ExpectedSnapshotVersion = 7, SourceDate = "2026-11-10",
            FetchedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            Rates = { new ClientRate { CurrencyId = "R01235", Code = "USD", Name = "Доллар США", Rate = "95.12340000" } }
        };
        var server = ImportRatesRequest.Parser.ParseFrom(client.ToByteArray());
        RatesContractValidator.Validate(server);
        // Stable v1 bytes, independent of the currently generated serializer.
        var fixture = ImportRatesRequest.Parser.ParseFrom(Convert.FromHexString(
            "0A2431313131313131312D313131312D343131312D383131312D31313131313131313131313110071A0A323032362D31" +
            "312D313022002A220A0652303132333512035553441A06446F6C6C6172220B39352E3132333430303030"));
        RatesContractValidator.Validate(fixture);
        Assert.Equal(7UL, fixture.ExpectedSnapshotVersion);
        Assert.NotNull(fixture.FetchedAt);
        Assert.Equal(0, fixture.FetchedAt.Seconds);
        Assert.Equal("95.12340000", Assert.Single(fixture.Rates).Rate);
        Assert.Equal(client.BatchId, server.BatchId);
        Assert.Equal(7UL, server.ExpectedSnapshotVersion);
        Assert.Equal("95.12340000", Assert.Single(server.Rates).Rate);
        Assert.NotEqual(client.GetType().Assembly, server.GetType().Assembly);
        var result = new ImportRatesResponse { Outcome = ImportOutcome.Applied, State = new ImportState
            { SnapshotVersion = 8, SourceDate = server.SourceDate, LoadedAt = server.FetchedAt } };
        RatesContractValidator.Validate(result);
        var reply = ClientResponse.Parser.ParseFrom(result.ToByteArray());
        Assert.Equal(8UL, reply.State.SnapshotVersion);
        Assert.Equal("currencyrates.finance.v1.RateImportService", RateImportService.Descriptor.FullName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("11111111111141118111111111111111")]
    [InlineData("not-a-uuid")]
    [InlineData(" 11111111-1111-4111-8111-111111111111 ")]
    [InlineData("\t11111111-1111-4111-8111-111111111111")]
    [InlineData("11111111-1111-4111-8111-111111111111\n")]
    [InlineData("11111111-+111-4111-8111-111111111111")]
    [InlineData("11111111-0x11-4111-8111-111111111111")]
    public void ImportRejectsNonCanonicalBatchIdentifiers(string batchId)
    {
        var request = ValidRequest();
        request.BatchId = batchId;
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
    }

    [Fact]
    public void ImportAcceptsCanonicalUppercaseHexBatchIdentifier()
    {
        var request = ValidRequest();
        request.BatchId = "ABCDEFAB-CDEF-4ABC-8ABC-ABCDEFABCDEF";
        RatesContractValidator.Validate(request);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1e2")]
    [InlineData("1,23")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1\n")]
    [InlineData("1000000000000")]
    [InlineData("1.000000001")]
    public void ImportRejectsInvalidRatesWithoutCultureDependentParsing(string rate)
    {
        var request = ValidRequest();
        request.Rates[0].Rate = rate;
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0.00000001")]
    [InlineData("999999999999.99999999")]
    public void ImportAcceptsBoundaryRatesUnderRussianCulture(string rate)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var request = ValidRequest();
            request.SourceDate = "2099-12-31";
            request.Rates[0].Rate = rate;
            RatesContractValidator.Validate(request);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void ImportRejectsMissingAndUnnormalizedFetchedTimestamp()
    {
        var request = ValidRequest();
        request.FetchedAt = null;
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
        request.FetchedAt = new Timestamp { Seconds = 0, Nanos = 1_000_000_000 };
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
        request.FetchedAt = new Timestamp { Seconds = 253_402_300_800 };
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
    }

    [Theory]
    [InlineData("2026-02-29")]
    [InlineData("10.11.2026")]
    [InlineData("2026-1-01")]
    [InlineData("")]
    public void ImportRejectsNonCalendarOrNonCanonicalDates(string date)
    {
        var request = ValidRequest();
        request.SourceDate = date;
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
    }

    [Fact]
    public void ImportRejectsEmptyOversizedAndDuplicateCurrencySets()
    {
        var request = ValidRequest();
        request.Rates.Add(request.Rates[0].Clone());
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
        request.Rates[1].CurrencyId = "R999";
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
        request.Rates.Clear();
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
        for (var i = 0; i < 1001; i++) { request.Rates.Add(new CurrencyRate()); }
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
    }

    [Theory]
    [InlineData("R1\n", "USD", "US Dollar")]
    [InlineData("R１２", "USD", "US Dollar")]
    [InlineData("R1", "usd", "US Dollar")]
    [InlineData("R1", "USD", "   ")]
    public void ImportRejectsInvalidCurrencyMetadata(string id, string code, string name)
    {
        var request = ValidRequest();
        request.Rates[0].CurrencyId = id;
        request.Rates[0].Code = code;
        request.Rates[0].Name = name;
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(request));
    }

    [Fact]
    public void StatePresenceRulesDistinguishUninitializedAndLoadedSnapshots()
    {
        RatesContractValidator.Validate(new ImportState());
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(new ImportState { SourceDate = "2026-11-10" }));
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(new ImportState { LoadedAt = new Timestamp() }));
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(new ImportState { SnapshotVersion = 1 }));
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(new ImportRatesResponse { Outcome = ImportOutcome.Applied }));
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(new ImportRatesResponse { State = new ImportState() }));
        Assert.Throws<ArgumentException>(() => RatesContractValidator.Validate(new ImportRatesResponse { Outcome = (ImportOutcome)999, State = new ImportState() }));
    }

    [Fact]
    public void HttpRatesKeepDecimalStringsNullUnavailableRatesAndUtcDate()
    {
        var value = new RatesResponse(new DateOnly(2026, 11, 10),
            new DateTimeOffset(2026, 11, 10, 17, 0, 0, TimeSpan.FromHours(3)),
            [new("R01235", "USD", "Dollar", 95.12340000m, true), new("R999", "ZZZ", "Unavailable", null, false)]);
        var options = new JsonSerializerOptions(JsonOptions) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(value, options));
        Assert.Equal("2026-11-10", json.RootElement.GetProperty("sourceDate").GetString());
        Assert.Equal("2026-11-10T14:00:00Z", json.RootElement.GetProperty("loadedAt").GetString());
        Assert.Equal("95.12340000", json.RootElement.GetProperty("items")[0].GetProperty("rate").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("items")[1].GetProperty("rate").ValueKind);
    }

    [Fact]
    public void OpenApiParsesAndRatesExampleMatchesDto()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "openapi.json"));
        var document = new OpenApiStringReader().Read(text, out var diagnostic);
        Assert.Empty(diagnostic.Errors);
        Assert.Equal(4, document.Paths.Count);
        var ratePattern = document.Components.Schemas["CurrencyRateResponse"].OneOf[0].Properties["rate"].Pattern;
        Assert.DoesNotMatch(ratePattern, "0.00000000");
        Assert.Matches(ratePattern, "0.00000001");
        using var json = JsonDocument.Parse(text);
        var example = json.RootElement.GetProperty("paths").GetProperty("/api/finance/rates")
            .GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("example");
        var dto = JsonSerializer.Deserialize<RatesResponse>(example.GetRawText(), JsonOptions)!;
        Assert.Equal(95.12340000m, Assert.Single(dto.Items).Rate);
        Assert.Equal(TimeSpan.Zero, dto.LoadedAt.Offset);
        var body = JsonSerializer.SerializeToElement(dto, JsonOptions);
        Assert.Equal(example.GetProperty("items")[0].GetProperty("rate").GetString(),
            body.GetProperty("items")[0].GetProperty("rate").GetString());
    }
}
