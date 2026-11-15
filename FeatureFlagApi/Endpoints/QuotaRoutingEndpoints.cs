using FeatureFlagApi.Dtos;
using FeatureFlagApi.Providers;

namespace FeatureFlagApi.Endpoints;

public static class QuotaRoutingEndpoints
{
    public static void MapQuotaRoutingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/payments/quota-routing-preview", PreviewRouting);
    }

    private static IResult PreviewRouting(QuotaRoutingPreviewRequest request)
    {
        if (request.CustomerIds is not { Length: > 0 and <= 1000 } customerIds ||
            customerIds.Any(string.IsNullOrWhiteSpace) ||
            request.NewProviderQuota < 0 || request.NewProviderQuota > customerIds.Length)
        {
            return Results.BadRequest(new
            {
                error = "Provide 1–1000 unique customer IDs and a new provider quota between 0 and the customer count."
            });
        }

        var normalizedIds = customerIds.Select(id => id.Trim()).ToArray();
        if (normalizedIds.Distinct(StringComparer.Ordinal).Count() != normalizedIds.Length)
        {
            return Results.BadRequest(new { error = "Customer IDs must be unique after trimming." });
        }

        // The quota belongs to this batch. Input order determines who receives each slot.
        // No feature flag, shared counter, or payment charge is involved in this preview.
        var assignments = normalizedIds.Select((customerId, index) => new
        {
            customerId,
            provider = index < request.NewProviderQuota
                ? NewPaymentProvider.Name
                : LegacyPaymentProvider.Name
        }).ToArray();

        return Results.Ok(new
        {
            total = assignments.Length,
            newProvider = request.NewProviderQuota,
            legacy = assignments.Length - request.NewProviderQuota,
            assignments
        });
    }
}
