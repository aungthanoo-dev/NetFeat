using FeatureFlagApi.Providers;
using Microsoft.AspNetCore.Mvc;

namespace FeatureFlagApi.Endpoints;

public static class TpsRoutingEndpoints
{
    public static void MapTpsRoutingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/payments/tps-routing-preview", PreviewRouting);
    }

    private static IResult PreviewRouting(
        [FromHeader(Name = "X-Demo-Customer-Id")] string? customerId,
        TpsRoutingLimiter limiter)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Results.BadRequest(new { error = "Provide X-Demo-Customer-Id." });
        }

        var admitted = limiter.TryAcquire();
        return Results.Ok(new
        {
            customerId = customerId.Trim(),
            provider = admitted ? NewPaymentProvider.Name : LegacyPaymentProvider.Name,
            reason = admitted ? "capacity-available" : "tps-limit-reached",
            newProviderLimitPerSecond = limiter.Limit
        });
    }
}
