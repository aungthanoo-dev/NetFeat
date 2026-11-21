using System.Net;
using System.Threading.Channels;
using RateLimitApi.Providers;
using Xunit.Abstractions;
using static RateLimitApi.IntegrationTests.PayoutApi;

namespace RateLimitApi.IntegrationTests;

public sealed class ConcurrencyLimiterTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Export_slots_are_shared_and_a_completed_export_frees_a_slot()
    {
        output.WriteLine("Expected   Two merchants hold both slots. The third export gets HTTP 429 and a completed export frees a slot.");
        var gate = new ExportGate();
        using var api = new PayoutApi(reconciliationDelay: gate);
        using var client = api.CreateDemoClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = deadline.Token;

        using var acceptedA = await PostAsync(client);
        var payoutA = await AssertAcceptedAsync(acceptedA);
        using var acceptedB = await PostAsync(client, MerchantB);
        var payoutB = await AssertAcceptedAsync(acceptedB);

        var first = SendAsync(client, HttpMethod.Get, ExportPath, MerchantA, cancellationToken: token);
        var releaseFirst = await gate.TakeStartedExportAsync(token);
        var second = SendAsync(client, HttpMethod.Get, ExportPath, MerchantB, cancellationToken: token);
        var releaseSecond = await gate.TakeStartedExportAsync(token);
        try
        {
            using var throttled = await SendAsync(client, HttpMethod.Get, ExportPath, cancellationToken: token);
            await AssertThrottledAsync(throttled, output, hasRetryAfter: false);

            releaseFirst.SetResult();
            using var firstResponse = await first;
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            Assert.Equal("text/csv", firstResponse.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", firstResponse.Content.Headers.ContentDisposition?.DispositionType);
            var csv = await firstResponse.Content.ReadAsStringAsync(token);
            Assert.Contains(payoutA.PayoutId.ToString(), csv);
            Assert.DoesNotContain(payoutB.PayoutId.ToString(), csv);

            var next = SendAsync(client, HttpMethod.Get, ExportPath, MerchantA, cancellationToken: token);
            var releaseNext = await gate.TakeStartedExportAsync(token);
            releaseNext.SetResult();
            using var nextResponse = await next;
            Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);

            releaseSecond.SetResult();
            using var secondResponse = await second;
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        }
        finally
        {
            releaseFirst.TrySetResult();
            releaseSecond.TrySetResult();
        }
    }

    private sealed class ExportGate : IReconciliationDelay
    {
        private readonly Channel<TaskCompletionSource> _started = Channel.CreateUnbounded<TaskCompletionSource>();

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _started.Writer.WriteAsync(release, cancellationToken);
            await release.Task.WaitAsync(cancellationToken);
        }

        internal ValueTask<TaskCompletionSource> TakeStartedExportAsync(CancellationToken cancellationToken)
            => _started.Reader.ReadAsync(cancellationToken);
    }
}
