namespace FeatureFlagApi.Dtos;

public sealed record QuotaRoutingPreviewRequest(
    string[]? CustomerIds,
    int NewProviderQuota);
