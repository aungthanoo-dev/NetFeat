using System.Globalization;
using System.Text;
using RateLimitApi.Dtos;
using RateLimitApi.Providers;
using RateLimitApi.RateLimiting;

namespace RateLimitApi.Endpoints;

public static class PayoutEndpoints
{
    public static void MapPayoutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var merchants = endpoints.MapGroup("")
            .AddEndpointFilter(async (context, next) =>
                DemoMerchant.GetId(context.HttpContext) is null
                    ? Results.BadRequest(new { error = "Provide X-Demo-Merchant-Id as merchant-a or merchant-b." })
                    : await next(context));

        merchants.MapPost("/payouts", CreatePayout)
            .RequireRateLimiting(MerchantRateLimiting.Submissions);

        merchants.MapGet("/payouts/{id:guid}", GetPayout)
            .RequireRateLimiting(MerchantRateLimiting.StatusChecks);

        merchants.MapGet("/reconciliation/export", ExportReconciliationAsync)
            .RequireRateLimiting(MerchantRateLimiting.Exports);
    }

    private static IResult CreatePayout(
        CreatePayoutRequest request, HttpContext context, PayoutStore store)
    {
        if (request.AmountInCents <= 0 || request.Currency != "USD")
        {
            return Results.BadRequest(new { error = "Provide a positive amount in cents and USD." });
        }

        var payout = store.Create(DemoMerchant.GetId(context)!, request);
        return Results.Accepted($"/payouts/{payout.PayoutId}", payout);
    }

    private static IResult GetPayout(Guid id, HttpContext context, PayoutStore store)
    {
        var payout = store.Find(DemoMerchant.GetId(context)!, id);
        return payout is null ? Results.NotFound() : Results.Ok(payout);
    }

    private static async Task<IResult> ExportReconciliationAsync(
        HttpContext context, PayoutStore store, IReconciliationDelay delay, CancellationToken cancellationToken)
    {
        await delay.WaitAsync(cancellationToken);

        var csv = new StringBuilder("payoutId,amountInCents,currency,status\r\n");
        foreach (var payout in store.List(DemoMerchant.GetId(context)!))
        {
            csv.Append(CultureInfo.InvariantCulture,
                $"{payout.PayoutId},{payout.AmountInCents},{payout.Currency},{payout.Status}\r\n");
        }

        return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", "reconciliation.csv");
    }
}
