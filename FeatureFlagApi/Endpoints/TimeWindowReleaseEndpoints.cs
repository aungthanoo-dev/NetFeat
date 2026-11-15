using FeatureFlagApi.Dtos;
using FeatureFlagApi.Providers;
using Microsoft.FeatureManagement;

namespace FeatureFlagApi.Endpoints;

public static class TimeWindowReleaseEndpoints
{
    public static void MapTimeWindowReleaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/payments/time-window-release", CreatePaymentAsync);
    }

    private static async Task<IResult> CreatePaymentAsync(
        CreatePaymentRequest request,
        IVariantFeatureManager flags,
        LegacyPaymentProvider legacyProvider,
        NewPaymentProvider newProvider)
    {
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

        IPaymentProvider provider = await flags.IsEnabledAsync("ScheduledPaymentProviderRelease")
            ? newProvider
            : legacyProvider;

        return Results.Ok(provider.Charge(request));
    }
}
