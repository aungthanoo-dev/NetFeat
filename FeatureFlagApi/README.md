# FeatureFlagApi

A simulated payment API demonstrating feature flags and related routing controls.
See the [research catalog](../README.md) for the solution overview and all samples.

## Running the demonstrations

Requires the .NET 11 SDK. From the solution root:

```powershell
dotnet restore NetFeat.sln
dotnet run --project FeatureFlagApi --launch-profile http
```

The `http` launch profile sets Development and listens at `http://localhost:5159`.
Open a linked `.http` file in Rider and run its requests. Script logs appear in
**Services > Response Handler**; assertions appear in the Tests tab. Collection
iteration in these samples sends requests sequentially. The concurrent sample
also requires Node.js 18+ on PATH and Rider HTTP Client shell-command support.

Feature definitions are in [appsettings.Development.json](appsettings.Development.json)
under `feature_management.feature_flags`. The TPS limit is in
[appsettings.json](appsettings.json). After changing demo configuration, restart
the API for a repeatable run. Restarting also resets in-memory limiter and refund
state. HTTP assertions assume the sample configuration unless stated otherwise.

[Program.cs](Program.cs) registers feature management and maps each demonstration
separately. Built-in targeting and time-window filters use the existing feature
management registration. Quota and TPS routing are custom controls, independent
of the flags. No provider calls an external payment service, and no outcomes are
persisted. `X-Demo-Customer-Id` is a demo identity; production identity should come
from authenticated context.

## Global release flag

**Purpose:** switch all payments between the legacy and new implementations.

**Implementation:** [PaymentEndpoints](Endpoints/PaymentEndpoints.cs) evaluates
`NewPaymentProvider` and calls the selected provider's `Charge` method.
`POST /payments` returns `paymentId`, `status`, and `provider`.

**Configuration:** `NewPaymentProvider.enabled` defaults to `false` (legacy).
Set it to `true` to select new for every request to this endpoint.

**Example:** run [ReleaseFlag.http](Requests/ReleaseFlag.http), toggle the flag,
restart, and repeat. The same payload changes from `provider: legacy` to `new`.
The payment payload used by this and the customer/scheduled release endpoints is:

```json
{
  "orderId": "order-101",
  "amount": 2599,
  "currency": "USD",
  "paymentMethodToken": "pm_demo_success"
}
```

An order ID, positive amount, USD currency, and supported token are required;
invalid input returns 400. `pm_demo_success` produces `succeeded`, while
`pm_demo_decline` produces `declined`. Valid declined payments still return 200.

## Customer-specific release

**Purpose:** release the new provider only to an explicit customer allowlist.

**Implementation:** [CustomerSpecificReleaseEndpoints](Endpoints/CustomerSpecificReleaseEndpoints.cs)
handles `POST /payments/customer-release`. It trims the required
`X-Demo-Customer-Id` header and passes it as `TargetingContext.UserId` when evaluating
`CustomerSpecificPaymentRelease`. The selected provider processes the simulated
payment. Missing or blank customer identity returns 400.

**Configuration:** `Microsoft.Targeting` has `Audience.Users: ["customer-001"]`,
empty groups, and `DefaultRolloutPercentage: 0`. Edit Users to change the allowlist.
Disabling or omitting the flag selects legacy for everyone.

**Example:** [CustomerSpecificRelease.http](Requests/CustomerSpecificRelease.http)
asserts new for `customer-001`, legacy for `customer-002`, and 400 for a missing
header. After disabling the flag, expect legacy for both customers; adjust the
selected-customer assertion when testing that configuration.

## Percentage rollout

**Purpose:** assign a stable proportion of customers to the new provider.

**Implementation:** [PaymentRoutingEndpoints](Endpoints/PaymentRoutingEndpoints.cs)
evaluates `NewPaymentProviderRouting` using the customer header as targeting
context. `POST /payments/routing-preview` returns the customer and provider;
it does not charge a payment. A missing or blank header returns 400.

**Configuration:** `Microsoft.Targeting` currently uses
`Audience.DefaultRolloutPercentage: 50`.

**Example:** [PercentageRollout.http](Requests/PercentageRollout.http) previews one
customer or iterates over 100 distinct IDs, logging counts and observed percentage.
50% is a population target, not an exact 50-of-100 quota. Stable IDs and unchanged
configuration give consistent assignment; repeated requests do not independently
randomize the provider. Larger sets generally give a closer observed percentage.

## Refund kill switch

**Purpose:** pause refunds through a manual flag or a simulated provider failure.

**Implementation:** [RefundEndpoints](Endpoints/RefundEndpoints.cs) checks
`RefundsEnabled` before using the singleton [RefundKillSwitch](Providers/RefundKillSwitch.cs).
`POST /payments/{paymentId:guid}/refund` returns a simulated successful refund or
503 when refunds are disabled. The demo's third admitted refund attempt throws a
caught exception, trips the in-memory `_tripped` latch, and returns 503. Later
attempts are blocked with 503.

**Configuration:** `RefundsEnabled.enabled: true` permits refunds unless the latch
is tripped. `RefundDemo.FailOnRequest: 3` enables the third-attempt failure in
Development; zero disables failure injection. The latch does not modify the flag
or configuration file. Changing the manual flag back to true cannot clear a
tripped latch; restart the API to reset it.

**Example:** restart, then run [KillSwitch.http](Requests/KillSwitch.http). The five
requests should produce `200, 200, 503, 503, 503`. Earlier refund calls consume
attempts, so start this batch from a fresh process. Setting the manual flag false
returns 503 immediately and prevents the failure counter from advancing.

## Payment retry experiment

**Purpose:** compare final payment success with and without a retry.

**Implementation:** [ExperimentEndpoints](Endpoints/ExperimentEndpoints.cs) handles
`POST /payments/experiment-preview`, accepts customer ID and demo token, and
resolves a named variant through `GetVariantAsync` with customer targeting context.
It returns experiment name, customer, variant, attempts, and final status. Blank
identity or unsupported tokens return 400.

**Configuration:** `PaymentRetryExperiment` allocates percentiles 0–50 to
`NoRetry` and 50–100 to `RetryOnce`, with seed `payment-retry-v1`. Both default
variants are `NoRetry`; disabling the flag assigns control to everyone.
Missing/unknown variants fall back to `NoRetry` with a warning. Assignment is stable
with unchanged identity, seed, and allocation; counts need not be exactly 50/50.

| Token | NoRetry | RetryOnce |
|---|---|---|
| `pm_demo_success` | Success, 1 attempt | Success, 1 attempt |
| `pm_demo_transient_failure` | Decline, 1 attempt | Success, 2 attempts |
| `pm_demo_decline` | Decline, 1 attempt | Decline, 1 attempt |

**Example:** [ExperimentFlag.http](Requests/ExperimentFlag.http) inspects one
assignment and runs 100 customers cycling through those inputs. It logs per-variant
counts, success rates, average attempts, invalid responses, and treatment-minus-control
success-rate difference in percentage points. Each batch resets its client-side
summary. Results are programmed simulations, not evidence of statistical
significance or real-world payment performance.

## Scheduled provider release

**Purpose:** enable the new provider only during a one-time UTC window.

**Implementation:** [TimeWindowReleaseEndpoints](Endpoints/TimeWindowReleaseEndpoints.cs)
evaluates `ScheduledPaymentProviderRelease` on `POST /payments/time-window-release`
and calls the corresponding simulated provider. It uses the shared payment payload
and validation described above.

**Configuration:** `Microsoft.TimeWindow` has Start `2030-01-01T12:00:00Z` and End
`2030-01-01T13:00:00Z`. Start is inclusive and End exclusive. The API server's clock
controls evaluation. Before/after the window, or with a disabled/missing flag,
legacy is selected. The initial window therefore selects legacy until its start.

**Example:** [TimeWindowRelease.http](Requests/TimeWindowRelease.http) has
`@expectedProvider = legacy`. To inspect each state, set both timestamps in the
future (legacy), straddle current UTC time (new), or set both in the past (legacy).
Restart after editing, update expectedProvider, and rerun. Completed requests do
not change provider when the window changes. The file also checks invalid input.

## Quota-based routing

**Purpose:** guarantee an exact count within a submitted batch.

**Implementation:** [QuotaRoutingEndpoints](Endpoints/QuotaRoutingEndpoints.cs)
handles `POST /payments/quota-routing-preview`. It assigns the first
`newProviderQuota` IDs to new and the remainder to legacy, returning assignments
and totals. This is an ordered preview, not a feature flag or persistent routing
assignment. Changing input order can change which customer receives a slot.

**Configuration:** supply `customerIds` and `newProviderQuota` in the request body.
Accepts 1–1000 nonblank IDs, unique after trimming, and a quota from zero through
the batch size. Invalid batches return 400. No flag configuration is involved.

**Example:** [QuotaRouting.http](Requests/QuotaRouting.http) sends 100 customers
with quota 50 and asserts exactly 50 new / 50 legacy. Each batch is independent;
this does not impose a per-second rate limit or charge payments.

## TPS-based routing

**Purpose:** protect new-provider capacity and immediately route overflow to legacy.

**Implementation:** [TpsRoutingEndpoints](Endpoints/TpsRoutingEndpoints.cs) handles
`POST /payments/tps-routing-preview` with a required customer header. A singleton
[TpsRoutingLimiter](Providers/TpsRoutingLimiter.cs) tracks admissions in the preceding
second under a lock. Available capacity selects new with `capacity-available`;
exhausted capacity selects legacy with `tps-limit-reached`. No waiting queue or
payment charge is involved. Missing identity returns 400.

**Configuration:** `TpsRouting.NewProviderLimitPerSecond: 20` in appsettings.json.
The limit must be positive and applies per API process, shared across all customers
calling this endpoint. It is independent of feature flags and not shared across
replicas. Restart after changing the limit.

**Example:** [TpsRouting.http](Requests/TpsRouting.http) runs 100 sequential previews.
Wait at least one second between runs. Requests spread over several seconds can
mostly use new because slots expire during the run; 20 TPS does not mean only 20
new assignments across an entire batch. Use the concurrent sample to observe overflow.

## Concurrent TPS burst

**Purpose:** demonstrate the same TPS limiter under a short concurrent burst.

**Implementation:** [TpsRoutingConcurrent.http](Requests/TpsRoutingConcurrent.http)
warms up the endpoint, then its response handler launches Node.js through Rider's
`execSync`. Node waits 1.1 seconds for earlier admissions to expire and sends 100
requests using `Promise.all`. Keep other endpoint traffic idle for a clean run.

**Configuration:** uses the same server-side TPS limit, plus the file's baseUrl.
Requires Node.js 18+ available to Rider and HTTP Client shell-command support.
It sends actual concurrent HTTP previews from Node; Rider's collection iteration
alone remains sequential.

**Example:** with limit 20, an otherwise idle limiter, and a burst completed within
one second, expect 20 new / 80 legacy. A slower burst can admit more new requests
as capacity recovers. Logs include counts, elapsed time, and measured routing TPS:

```text
measured TPS = successful previews / (elapsed milliseconds / 1000)
```

For example, 100 successes in 200 ms equals 500 previews/sec averaged over that
burst. This includes both providers and does not establish sustained capacity or
measure real payment processing. If Node returns no output, check `node --version`
in Rider's terminal and shell-command availability; non-JSON output is reported
by the handler for diagnosis.
