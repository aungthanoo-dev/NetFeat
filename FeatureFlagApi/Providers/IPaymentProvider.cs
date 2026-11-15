using FeatureFlagApi.Dtos;

namespace FeatureFlagApi.Providers;

public interface IPaymentProvider
{
    CreatePaymentResponse Charge(CreatePaymentRequest request);
}