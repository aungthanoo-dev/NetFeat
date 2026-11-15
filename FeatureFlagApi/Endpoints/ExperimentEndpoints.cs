using FeatureFlagApi.Dtos;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace FeatureFlagApi.Endpoints;

public static class ExperimentEndpoints
{
    public static void MapExperimentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/payments/experiment-preview", PreviewExperimentAsync);
    }

    private static async Task<IResult> PreviewExperimentAsync(
        ExperimentPreviewRequest request,
        IVariantFeatureManager flags,
        ILoggerFactory loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId) ||
            request.PaymentMethodToken is not ("pm_demo_success" or "pm_demo_transient_failure" or "pm_demo_decline"))
        {
            return Results.BadRequest(new { error = "Provide a customer ID and supported demo token." });
        }

        const string experiment = "PaymentRetryExperiment";
        var targetingContext = new TargetingContext
        {
            UserId = request.CustomerId.Trim(),
            Groups = Array.Empty<string>()
        };

        var assigned = await flags.GetVariantAsync(
            experiment,
            targetingContext);

        var variant = assigned?.Name;
        if (variant is not ("NoRetry" or "RetryOnce"))
        {
            loggerFactory.CreateLogger("ExperimentEndpoints")
                .LogWarning("Missing or unknown variant {Variant}; using NoRetry", variant);
            variant = "NoRetry";
        }

        var retry = variant == "RetryOnce" &&
            request.PaymentMethodToken == "pm_demo_transient_failure";
        var status = request.PaymentMethodToken == "pm_demo_success" || retry
            ? "succeeded"
            : "declined";

        return Results.Ok(new
        {
            experiment,
            customerId = targetingContext.UserId,
            variant,
            attempts = retry ? 2 : 1,
            status
        });
    }
}
