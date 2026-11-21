using FeatureFlagApi.Dtos;
using Xunit.Abstractions;
using static FeatureFlagApi.IntegrationTests.PaymentApi;

namespace FeatureFlagApi.IntegrationTests;

public sealed class PaymentRetryExperimentTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Experiment_assigns_both_variants_and_treats_tokens_as_expected()
    {
        output.Start($"Payment retry experiment with {Customers.Length} customers",
            "Both variants are assigned and repeat previews keep the same variant.");
        output.Expected("Success token succeeds and a permanent decline stays declined, each in one attempt.");
        output.Expected("A transient failure declines in one attempt with NoRetry and succeeds in two with RetryOnce.");
        using var api = new PaymentApi();
        using var client = api.CreateClient();
        string[] tokens = ["pm_demo_success", "pm_demo_transient_failure", "pm_demo_decline"];

        var results = await Task.WhenAll(Customers.Select((customer, index) =>
            PreviewAsync(customer, tokens[index % tokens.Length])));

        var controlRate = await CheckVariantAsync("NoRetry");
        var treatmentRate = await CheckVariantAsync("RetryOnce");
        output.Details($"Observed success rate difference, RetryOnce minus NoRetry: " +
            $"{(treatmentRate - controlRate) * 100:F1} percentage points.");

        async Task<double> CheckVariantAsync(string variant)
        {
            var group = results.Where(result => result.Variant == variant).ToArray();
            output.Actual($"{variant}: {group.Length} customers assigned.");
            Assert.NotEmpty(group);
            var successes = group.Count(result => result.Status == "succeeded");
            var successRate = (double)successes / group.Length;
            output.Details($"{variant}: {successes} successes " +
                $"({successRate:P1}), {group.Average(result => result.Attempts):F2} average attempts.");
            var customer = group[0].CustomerId;
            foreach (var token in tokens)
            {
                var result = await PreviewAsync(customer, token, reportResult: true);
                Assert.Equal(variant, result.Variant);
            }

            return successRate;
        }

        async Task<ExperimentPreview> PreviewAsync(string customer, string token, bool reportResult = false)
        {
            using var response = await PostAsync(client, "/experiment-preview",
                body: new ExperimentPreviewRequest(customer, token));
            var result = await ReadOkAsync<ExperimentPreview>(response, output);
            var retried = result.Variant == "RetryOnce" && token == "pm_demo_transient_failure";
            var expectedAttempts = retried ? 2 : 1;
            var expectedStatus = token == "pm_demo_success" || retried ? "succeeded" : "declined";
            if (reportResult || result.Attempts != expectedAttempts || result.Status != expectedStatus)
            {
                output.Actual($"{result.Variant}, {customer} with {token}: " +
                    $"{result.Status}, {result.Attempts} attempt(s).");
            }

            Assert.Equal(customer, result.CustomerId);
            Assert.Equal("PaymentRetryExperiment", result.Experiment);
            Assert.Contains(result.Variant, new[] { "NoRetry", "RetryOnce" });
            Assert.Equal(expectedAttempts, result.Attempts);
            Assert.Equal(expectedStatus, result.Status);
            return result;
        }
    }

    private sealed record ExperimentPreview(string Experiment, string CustomerId, string Variant, int Attempts, string Status);
}
