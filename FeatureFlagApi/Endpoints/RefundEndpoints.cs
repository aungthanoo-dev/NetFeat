using Microsoft.FeatureManagement;

using FeatureFlagApi.Providers;

namespace FeatureFlagApi.Endpoints;

public static class RefundEndpoints
{
    public static void MapRefundEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/payments/{paymentId:guid}/refund", RefundPaymentAsync);
    }
    
    private static async Task<IResult> RefundPaymentAsync(
        Guid paymentId,
        IVariantFeatureManager flags,
        RefundKillSwitch killSwitch,
        ILoggerFactory loggerFactory)
    {
        if (!await flags.IsEnabledAsync("RefundsEnabled"))
        {
            return Results.Problem(
                title: "Refunds are temporarily unavailable",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            if (!killSwitch.TryBeginRefund())
            {
                return Results.Problem(
                    title: "Refunds are temporarily unavailable",
                    detail: "The demo kill switch is tripped. Restart the API to reset it.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }
        catch (SimulatedRefundException exception)
        {
            loggerFactory.CreateLogger("RefundEndpoints")
                .LogError(exception, "Simulated provider failure tripped the refund kill switch.");
            return Results.Problem(
                title: "Refund provider failed; refunds have been paused",
                detail: "The demo exception tripped the kill switch. Restart the API to reset it.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new
        {
            paymentId,
            refundId = Guid.NewGuid(),
            status = "succeeded"
        });
    }
}
