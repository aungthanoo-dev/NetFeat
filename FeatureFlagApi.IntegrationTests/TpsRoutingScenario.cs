using System.Diagnostics;
using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

internal static class TpsRoutingScenario
{
    internal static async Task AssertOverflowAndRecoveryAsync(ITestOutputHelper output, bool concurrent)
    {
        var mode = concurrent ? "Concurrent" : "Sequential";
        var order = concurrent ? "" : " The first 20 requests use new.";
        output.Start($"{mode} TPS burst with {Customers.Length} requests",
            "20 new, 80 legacy; legacy at 999 ms, new at 1,000 ms on the limiter clock." + order);
        var clock = new TestClock();
        using var api = new PaymentApi(clock: clock);
        using var client = api.CreateClient();
        var results = new List<TpsPreview>();
        var stopwatch = Stopwatch.StartNew();

        if (concurrent)
        {
            results.AddRange(await Task.WhenAll(Customers.Select(customer => PreviewAsync(customer))));
        }
        else
        {
            foreach (var customer in Customers)
            {
                results.Add(await PreviewAsync(customer));
            }
        }

        stopwatch.Stop();
        var newCount = results.Count(route => route.Provider == "new");
        var legacyCount = results.Count(route => route.Provider == "legacy");
        output.Actual($"{results.Count} successful previews, {newCount} new, {legacyCount} legacy.");
        output.Details($"In-process batch duration: {stopwatch.Elapsed.TotalMilliseconds:F1} ms.");
        if (!concurrent)
        {
            var firstNew = results.Take(20).Count(route => route.Provider == "new");
            output.Actual($"First 20 previews: {firstNew} new, {20 - firstNew} legacy.");
            Assert.All(results.Take(20), route => Assert.Equal("new", route.Provider));
        }

        Assert.Equal(20, newCount);
        Assert.Equal(80, legacyCount);

        clock.Advance(TimeSpan.FromMilliseconds(999));
        var beforeRecovery = await PreviewAsync(Customers[0]);
        output.Actual($"At 999 ms on the limiter clock: {beforeRecovery.Provider} ({beforeRecovery.Reason}).");
        Assert.Equal("legacy", beforeRecovery.Provider);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var afterRecovery = await PreviewAsync(Customers[0]);
        output.Actual($"At 1,000 ms on the limiter clock: {afterRecovery.Provider} ({afterRecovery.Reason}).");
        Assert.Equal("new", afterRecovery.Provider);

        async Task<TpsPreview> PreviewAsync(string customer)
        {
            using var response = await PostAsync(client, "/tps-routing-preview", customer);
            var result = await ReadOkAsync<TpsPreview>(response, output);
            Assert.Equal(customer, result.CustomerId);
            Assert.Equal(20, result.NewProviderLimitPerSecond);
            Assert.Contains(result.Provider, new[] { "new", "legacy" });
            Assert.Equal(result.Provider == "new" ? "capacity-available" : "tps-limit-reached", result.Reason);
            return result;
        }
    }

    private sealed record TpsPreview(string CustomerId, string Provider, string Reason, int NewProviderLimitPerSecond);

    private sealed class TestClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref _timestamp, elapsed.Ticks);
    }
}
