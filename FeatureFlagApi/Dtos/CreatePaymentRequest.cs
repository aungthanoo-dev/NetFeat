namespace FeatureFlagApi.Dtos;

public sealed record CreatePaymentRequest(
    string OrderId,
    long Amount,
    string Currency,
    string PaymentMethodToken);