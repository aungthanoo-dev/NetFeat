using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace RateLimitApi.RateLimiting;

internal static class MerchantRateLimiting
{
    internal const string Submissions = "payout-submit";
    internal const string StatusChecks = "payout-status";
    internal const string Exports = "reconciliation-export";

    internal static void AddMerchantRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitSettings>()
            .BindConfiguration("RateLimiting")
            .Validate(settings => Enum.IsDefined(settings.StatusPolicy),
                "RateLimiting:StatusPolicy must be FixedWindow or SlidingWindow.")
            .ValidateOnStart();

        services.AddRateLimiter();
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitSettings>>((options, configured) =>
        {
            var settings = configured.Value;
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            options.AddPolicy(Submissions, context => ForMerchant(context, merchantId =>
                RateLimitPartition.GetTokenBucketLimiter(merchantId, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = settings.SubmissionTokenLimit,
                    TokensPerPeriod = settings.SubmissionTokensPerPeriod,
                    ReplenishmentPeriod = settings.SubmissionReplenishmentPeriod,
                    AutoReplenishment = true,
                    QueueLimit = 0
                })));

            options.AddPolicy(StatusChecks, context => ForMerchant(context, merchantId =>
                settings.StatusPolicy == StatusWindow.SlidingWindow
                    ? RateLimitPartition.GetSlidingWindowLimiter(merchantId, _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = settings.StatusPermitLimit,
                        Window = settings.StatusWindowLength,
                        SegmentsPerWindow = settings.StatusSegmentsPerWindow,
                        AutoReplenishment = true,
                        QueueLimit = 0
                    })
                    : RateLimitPartition.GetFixedWindowLimiter(merchantId, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.StatusPermitLimit,
                        Window = settings.StatusWindowLength,
                        AutoReplenishment = true,
                        QueueLimit = 0
                    })));

            options.AddPolicy(Exports, context => ForMerchant(context, _ =>
                RateLimitPartition.GetConcurrencyLimiter(Exports, _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = settings.ExportPermitLimit,
                    QueueLimit = 0
                })));
        });
    }

    private static RateLimitPartition<string> ForMerchant(
        HttpContext context, Func<string, RateLimitPartition<string>> createPartition)
        => DemoMerchant.GetId(context) is { } merchantId
            ? createPartition(merchantId)
            : RateLimitPartition.GetNoLimiter("invalid-merchant");

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too Many Requests",
            detail: "The rate limit for this endpoint was exceeded.")
            .ExecuteAsync(context.HttpContext);
    }
}
