namespace FeatureFlagApi.Dtos;

public sealed record ExperimentPreviewRequest(
    string? CustomerId,
    string? PaymentMethodToken);
