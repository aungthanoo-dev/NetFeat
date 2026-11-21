namespace RateLimitApi.Dtos;

public sealed record CreatePayoutRequest(long AmountInCents, string Currency);
