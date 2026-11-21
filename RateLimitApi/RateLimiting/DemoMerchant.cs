namespace RateLimitApi.RateLimiting;

internal static class DemoMerchant
{
    internal static string? GetId(HttpContext context)
    {
        var values = context.Request.Headers["X-Demo-Merchant-Id"];
        return values.Count == 1 && values[0] is "merchant-a" or "merchant-b" ? values[0] : null;
    }
}
