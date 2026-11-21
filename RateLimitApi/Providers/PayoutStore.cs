using System.Collections.Concurrent;
using RateLimitApi.Dtos;

namespace RateLimitApi.Providers;

internal sealed class PayoutStore
{
    private readonly ConcurrentDictionary<(string MerchantId, Guid PayoutId), PayoutResponse> _payouts = new();

    internal PayoutResponse Create(string merchantId, CreatePayoutRequest request)
    {
        var payout = new PayoutResponse(Guid.NewGuid(), request.AmountInCents, request.Currency, "pending");
        _payouts[(merchantId, payout.PayoutId)] = payout;
        return payout;
    }

    internal PayoutResponse? Find(string merchantId, Guid payoutId)
        => _payouts.GetValueOrDefault((merchantId, payoutId));

    internal PayoutResponse[] List(string merchantId)
        => _payouts.ToArray()
            .Where(entry => entry.Key.MerchantId == merchantId)
            .Select(entry => entry.Value)
            .OrderBy(payout => payout.PayoutId)
            .ToArray();
}
