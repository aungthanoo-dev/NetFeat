namespace FeatureFlagApi.Dtos;

public sealed record CreatePaymentResponse(
    Guid PaymentId,
    string Status,
    string Provider);