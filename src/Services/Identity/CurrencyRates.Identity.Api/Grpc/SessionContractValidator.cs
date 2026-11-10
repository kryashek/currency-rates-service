using CurrencyRates.Contracts.Identity.V1;

namespace CurrencyRates.Identity.Api.Grpc;

// Shape validation only. Session existence, ownership and expiry are Identity use cases.
public static class SessionContractValidator
{
    public static void Validate(ValidateSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireUuid(request.UserId, nameof(request.UserId));
        RequireUuid(request.SessionId, nameof(request.SessionId));
    }

    private static void RequireUuid(string value, string field)
    {
        // TryParseExact also accepts surrounding whitespace and legacy component forms.
        if (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty ||
            !string.Equals(value, id.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A non-empty UUID in D format is required.", field);
        }
    }
}
