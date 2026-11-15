using FeatureFlagApi.Dtos;

namespace FeatureFlagApi.Providers;

public sealed class LegacyPaymentProvider : IPaymentProvider
{
    public const string Name = "legacy";
    
    public CreatePaymentResponse Charge(CreatePaymentRequest request)
    {
        var status = request.PaymentMethodToken == "pm_demo_success"
            ? "succeeded"
            : "declined";

        return new CreatePaymentResponse(Guid.NewGuid(), status, Name);
    }
}