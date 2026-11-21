using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using RateLimitApi.Dtos;
using RateLimitApi.Providers;
using Xunit.Abstractions;

namespace RateLimitApi.IntegrationTests;

internal sealed class PayoutApi(
    Dictionary<string, string?>? rateSettings = null,
    IReconciliationDelay? reconciliationDelay = null) : WebApplicationFactory<Program>
{
    internal const string MerchantA = "merchant-a";
    internal const string MerchantB = "merchant-b";
    internal const string ExportPath = "/reconciliation/export";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging =>
        {
            if (OperatingSystem.IsWindows())
            {
                logging.AddFilter<EventLogLoggerProvider>(_ => false);
            }
        });

        if (rateSettings is not null)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                rateSettings.Select(setting => KeyValuePair.Create($"RateLimiting:{setting.Key}", setting.Value))));
        }

        if (reconciliationDelay is not null)
        {
            builder.ConfigureServices(services => services.Replace(
                ServiceDescriptor.Singleton<IReconciliationDelay>(reconciliationDelay)));
        }
    }

    internal HttpClient CreateDemoClient()
        => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    internal static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, string? merchant = MerchantA,
        HttpContent? content = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (merchant is not null)
        {
            request.Headers.Add("X-Demo-Merchant-Id", merchant);
        }

        return await client.SendAsync(request, cancellationToken);
    }

    internal static Task<HttpResponseMessage> PostAsync(
        HttpClient client, string? merchant = MerchantA, long amountInCents = 2599, string currency = "USD",
        CancellationToken cancellationToken = default)
        => SendAsync(client, HttpMethod.Post, "/payouts", merchant,
            JsonContent.Create(new CreatePayoutRequest(amountInCents, currency)), cancellationToken);

    internal static Task<HttpResponseMessage> GetStatusAsync(
        HttpClient client, Uri location, string merchant = MerchantA, CancellationToken cancellationToken = default)
        => SendAsync(client, HttpMethod.Get, location.OriginalString, merchant, cancellationToken: cancellationToken);

    internal static async Task<PayoutResponse> AssertAcceptedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var payout = Assert.IsType<PayoutResponse>(await response.Content.ReadFromJsonAsync<PayoutResponse>());
        Assert.NotEqual(Guid.Empty, payout.PayoutId);
        Assert.Equal("pending", payout.Status);
        Assert.Equal($"/payouts/{payout.PayoutId}", response.Headers.Location?.OriginalString);
        return payout;
    }

    internal static async Task AssertThrottledAsync(
        HttpResponseMessage response, ITestOutputHelper output, bool hasRetryAfter = true)
    {
        output.WriteLine($"Actual     HTTP {(int)response.StatusCode}, Retry-After={response.Headers.RetryAfter?.ToString() ?? "absent"}.");
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = Assert.IsType<ProblemDetails>(await response.Content.ReadFromJsonAsync<ProblemDetails>());
        Assert.Equal(429, problem.Status);
        Assert.Equal("Too Many Requests", problem.Title);
        if (hasRetryAfter)
        {
            Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        }
        else
        {
            Assert.Null(response.Headers.RetryAfter);
        }
    }

    internal static async Task WaitForRecoveryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send, HttpStatusCode expected,
        TimeSpan timeout, ITestOutputHelper output)
    {
        using var deadline = new CancellationTokenSource(timeout);

        try
        {
            while (true)
            {
                using var response = await send(deadline.Token);
                if (response.StatusCode == expected)
                {
                    output.WriteLine($"Actual     Recovered with HTTP {(int)response.StatusCode}.");
                    return;
                }

                Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
                await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            Assert.Fail($"Expected HTTP {(int)expected} within {timeout} after throttling.");
        }
    }
}
