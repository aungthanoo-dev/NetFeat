using RateLimitApi.Endpoints;
using RateLimitApi.Providers;
using RateLimitApi.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<PayoutStore>();
builder.Services.AddSingleton<IReconciliationDelay, ReconciliationDelay>();
builder.Services.AddMerchantRateLimiting();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.MapPayoutEndpoints();

app.Run();

public partial class Program { }
