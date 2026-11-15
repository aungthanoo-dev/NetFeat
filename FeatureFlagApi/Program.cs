using FeatureFlagApi.Endpoints;
using FeatureFlagApi.Providers;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddFeatureManagement();
builder.Services.AddSingleton<LegacyPaymentProvider>();
builder.Services.AddSingleton<NewPaymentProvider>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<TpsRoutingLimiter>();
builder.Services.AddSingleton<RefundKillSwitch>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPaymentEndpoints();
app.MapTimeWindowReleaseEndpoints();
app.MapPaymentRoutingEndpoints();
app.MapQuotaRoutingEndpoints();
app.MapTpsRoutingEndpoints();
app.MapRefundEndpoints();
app.MapExperimentEndpoints();
app.MapCustomerSpecificReleaseEndpoints();

app.Run();



