using FeatureFlagApi.Dtos;
using FeatureFlagApi.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace FeatureFlagApi.Endpoints;

public static class CombinedFilterReleaseEndpoints
{
    public static void MapCombinedFilterReleaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/payments/combined-filter-release", CreatePaymentAsync);
    }

    private static async Task<IResult> CreatePaymentAsync(
        CreatePaymentRequest request,
        [FromHeader(Name = "X-Demo-Customer-Id")] string? customerId,
        IVariantFeatureManager flags,
        LegacyPaymentProvider legacyProvider,
        NewPaymentProvider newProvider)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Results.BadRequest(new { error = "Provide X-Demo-Customer-Id." });
        }

        if (string.IsNullOrWhiteSpace(request.OrderId) ||
            request.Amount <= 0 ||
            request.Currency != "USD" ||
            request.PaymentMethodToken is not ("pm_demo_success" or "pm_demo_decline"))
        {
            return Results.BadRequest(new
            {
                error = "Provide an order ID, a positive amount, USD, and a supported demo token."
            });
        }

        var targetingContext = new TargetingContext
        {
            UserId = customerId.Trim(),
            Groups = Array.Empty<string>()
        };

        var useNewProvider = await flags.IsEnabledAsync(
            "CombinedPaymentProviderRelease",
            targetingContext);

        IPaymentProvider provider = useNewProvider
            ? newProvider
            : legacyProvider;

        return Results.Ok(provider.Charge(request));
    }
}

