using System.Net;
using System.Net.Http.Json;
using FeatureFlagApi.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Xunit.Abstractions;

namespace FeatureFlagApi.IntegrationTests;

internal sealed class PaymentApi(
    string? flagId = null,
    Dictionary<string, string?>? flagSettings = null,
    TimeProvider? clock = null) : WebApplicationFactory<Program>
{
    internal static readonly string[] Customers = Enumerable.Range(1, 100)
        .Select(index => $"customer-{index:000}")
        .ToArray();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging =>
        {
            if (OperatingSystem.IsWindows())
            {
                logging.AddFilter<EventLogLoggerProvider>(level => false);
            }
        });
        if (flagId is not null && flagSettings is not null)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var existing = configuration.Build();
                using var configurationSnapshot = (IDisposable)existing;
                var flag = existing.GetSection("feature_management:feature_flags").GetChildren()
                    .Single(section => section["id"] == flagId);
                configuration.AddInMemoryCollection(flagSettings.Select(setting =>
                    KeyValuePair.Create($"{flag.Path}:{setting.Key}", setting.Value)));
            });
        }

        if (clock is not null)
        {
            builder.ConfigureServices(services => services.Replace(
                ServiceDescriptor.Singleton<TimeProvider>(clock)));
        }
    }

    internal static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, string path, string? customer = null, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/payments{path}");
        if (customer is not null)
        {
            request.Headers.Add("X-Demo-Customer-Id", customer);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    internal static CreatePaymentRequest Payment(string orderId = "order-101", int amount = 2599)
        => new(orderId, amount, "USD", "pm_demo_success");

    internal static async Task<T> ReadOkAsync<T>(
        HttpResponseMessage response, ITestOutputHelper? output = null) where T : class
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            output?.Actual($"HTTP {(int)response.StatusCode} from {response.RequestMessage?.RequestUri?.AbsolutePath}");
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<T>(await response.Content.ReadFromJsonAsync<T>());
    }

    internal static async Task AssertPaymentAsync(
        ITestOutputHelper output, HttpClient client, string scenario, string path, string expected,
        string? customer = null)
    {
        output.Start(scenario, $"HTTP 200, provider={expected}, status=succeeded, payment ID present.");
        var provider = await PayAsync(client, path, customer, output: output);
        Assert.Equal(expected, provider);
    }

    internal static async Task AssertMissingCustomerAsync(ITestOutputHelper output, string path, object? body = null)
    {
        output.Start($"POST /payments{path} without customer identity", "HTTP 400 Bad Request.");
        using var api = new PaymentApi();
        using var client = api.CreateClient();
        using var response = await PostAsync(client, path, body: body);
        output.Actual($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    internal static async Task<string> PayAsync(
        HttpClient client, string path, string? customer = null, string orderId = "order-101",
        ITestOutputHelper? output = null, bool reportSuccess = true)
    {
        using var response = await PostAsync(client, path, customer, Payment(orderId));
        var result = await ReadOkAsync<CreatePaymentResponse>(response, output);
        if (reportSuccess || result.Status != "succeeded" || result.PaymentId == Guid.Empty ||
            result.Provider is not ("new" or "legacy"))
        {
            var paymentId = result.PaymentId == Guid.Empty ? "missing" : "present";
            output?.Actual($"HTTP {(int)response.StatusCode}, provider={result.Provider}, " +
                $"status={result.Status}, payment ID {paymentId}.");
        }

        Assert.Equal("succeeded", result.Status);
        Assert.NotEqual(Guid.Empty, result.PaymentId);
        Assert.Contains(result.Provider, new[] { "new", "legacy" });
        return result.Provider;
    }
}
