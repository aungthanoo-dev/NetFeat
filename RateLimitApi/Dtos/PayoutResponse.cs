namespace RateLimitApi.Dtos;

public sealed record PayoutResponse(Guid PayoutId, long AmountInCents, string Currency, string Status);
