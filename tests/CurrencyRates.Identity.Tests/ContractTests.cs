extern alias SessionClient;

using System.Text.Json;
using CurrencyRates.Identity.Api.Contracts;
using CurrencyRates.Identity.Api.Grpc;
using Google.Protobuf;
using Microsoft.OpenApi.Readers;
using Xunit;
using ServerRequest = CurrencyRates.Contracts.Identity.V1.ValidateSessionRequest;
using ServerResponse = CurrencyRates.Contracts.Identity.V1.ValidateSessionResponse;
using ClientRequest = SessionClient::CurrencyRates.Contracts.Identity.V1.ValidateSessionRequest;
using ClientResponse = SessionClient::CurrencyRates.Contracts.Identity.V1.ValidateSessionResponse;

namespace CurrencyRates.Identity.Tests;

public sealed class ContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void IndependentlyGeneratedClientAndServerPreserveSessionIdentifiers()
    {
        var client = new ClientRequest
        {
            UserId = "11111111-1111-4111-8111-111111111111",
            SessionId = "22222222-2222-4222-8222-222222222222"
        };
        var server = ServerRequest.Parser.ParseFrom(client.ToByteArray());
        SessionContractValidator.Validate(server);
        // Stable v1 bytes: catches field renumbering even if both sides regenerate.
        var fixture = ServerRequest.Parser.ParseFrom(Convert.FromHexString(
            "0A2431313131313131312D313131312D343131312D383131312D31313131313131313131313112243232323232323232" +
            "2D323232322D343232322D383232322D323232323232323232323232"));
        SessionContractValidator.Validate(fixture);
        Assert.Equal(client.UserId, fixture.UserId);
        Assert.Equal(client.SessionId, fixture.SessionId);
        Assert.Equal(client.UserId, server.UserId);
        Assert.Equal(client.SessionId, server.SessionId);
        Assert.NotEqual(client.GetType().Assembly, server.GetType().Assembly);
        Assert.True(ClientResponse.Parser.ParseFrom(new ServerResponse { IsValid = true }.ToByteArray()).IsValid);
        Assert.False(ClientResponse.Parser.ParseFrom(new ServerResponse().ToByteArray()).IsValid);
        Assert.Equal("currencyrates.identity.v1.SessionService",
            CurrencyRates.Contracts.Identity.V1.SessionService.Descriptor.FullName);
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
    public void SessionRequestRejectsMissingOrMalformedIdentifiers(string invalid)
    {
        var request = new ServerRequest { UserId = invalid, SessionId = Guid.NewGuid().ToString("D") };
        Assert.Throws<ArgumentException>(() => SessionContractValidator.Validate(request));
        request.UserId = Guid.NewGuid().ToString("D");
        request.SessionId = invalid;
        Assert.Throws<ArgumentException>(() => SessionContractValidator.Validate(request));
    }

    [Fact]
    public void SessionRequestAcceptsCanonicalUppercaseHexIdentifiers()
    {
        var request = new ServerRequest
        {
            UserId = "A1111111-B111-4111-8111-C11111111111",
            SessionId = "D2222222-E222-4222-8222-F22222222222"
        };
        SessionContractValidator.Validate(request);
    }

    [Fact]
    public void CredentialTransportPreservesSignificantPasswordSpaces()
    {
        const string payload = """{"name":" Alice ","password":"  PaSsword123!  "}""";
        var register = JsonSerializer.Deserialize<RegisterRequest>(payload, JsonOptions)!;
        var login = JsonSerializer.Deserialize<LoginRequest>(payload, JsonOptions)!;
        Assert.Equal("  PaSsword123!  ", register.Password);
        Assert.Equal(register.Password, login.Password);
        Assert.Equal(" Alice ", register.Name);
        Assert.DoesNotContain(register.Password, register.ToString()!);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RegisterRequest>("{}", JsonOptions));
    }

    [Fact]
    public void LoginResponseWritesUtcTimeWithZAndUuidString()
    {
        var value = new LoginResponse("example-token", "Bearer",
            new DateTimeOffset(2026, 11, 10, 18, 0, 0, TimeSpan.FromHours(3)),
            Guid.Parse("11111111-1111-4111-8111-111111111111"));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        Assert.Equal("2026-11-10T15:00:00Z", json.RootElement.GetProperty("expiresAt").GetString());
        Assert.Equal(value.UserId.ToString("D"), json.RootElement.GetProperty("userId").GetString());
    }

    [Fact]
    public void OpenApiParsesAndLoginExampleMatchesDto()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "openapi.json"));
        var document = new OpenApiStringReader().Read(text, out var diagnostic);
        Assert.Empty(diagnostic.Errors);
        Assert.Equal(3, document.Paths.Count);
        using var json = JsonDocument.Parse(text);
        var example = json.RootElement.GetProperty("paths").GetProperty("/api/auth/login")
            .GetProperty("post").GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("example");
        var dto = JsonSerializer.Deserialize<LoginResponse>(example.GetRawText(), JsonOptions)!;
        Assert.Equal("Bearer", dto.TokenType);
        Assert.Equal(TimeSpan.Zero, dto.ExpiresAt.Offset);
        var schema = json.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("LoginResponse");
        Assert.Equal(schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).Order(),
            JsonDocument.Parse(JsonSerializer.Serialize(dto, JsonOptions)).RootElement.EnumerateObject().Select(x => x.Name).Order());
    }
}
