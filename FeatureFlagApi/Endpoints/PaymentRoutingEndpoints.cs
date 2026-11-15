using FeatureFlagApi.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace FeatureFlagApi.Endpoints;

public static class PaymentRoutingEndpoints
{
    public static void MapPaymentRoutingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/payments/routing-preview", PreviewRoutingAsync);
    }
    
    private static async Task<IResult> PreviewRoutingAsync(
        [FromHeader(Name = "X-Demo-Customer-Id")] string? customerId,
        IVariantFeatureManager flags)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Results.BadRequest(new { error = "Provide X-Demo-Customer-Id." });
        }

        var targetingContext = new TargetingContext
        {
            UserId = customerId.Trim(),
            Groups = Array.Empty<string>()
        };

        var useNewProvider = await flags.IsEnabledAsync(
            "NewPaymentProviderRouting",
            targetingContext);

        return Results.Ok(new
        {
            customerId = targetingContext.UserId,
            provider = useNewProvider
                ? NewPaymentProvider.Name
                : LegacyPaymentProvider.Name
        });
    }
}