using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

public sealed class GlobalReleaseFlagTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true, "new")]
    [InlineData(false, "legacy")]
    public async Task Global_release_routes_payments_by_flag(bool enabled, string expected)
    {
        using var api = new PaymentApi("NewPaymentProvider", new() { ["enabled"] = enabled.ToString() });
        using var client = api.CreateClient();
        await AssertPaymentAsync(output, client, $"Global provider release with flag enabled={enabled}", "", expected);
    }
}
