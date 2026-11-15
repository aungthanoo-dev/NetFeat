# NetFeat

NetFeat is a research solution for exploring feature management in a simulated
payment API. Its demonstrations compare release flags, customer targeting,
percentage rollout, experiments, scheduled availability, and operational controls.
Quota and TPS routing provide related examples of allocation and capacity control.
All payment providers and refunds are simulated; no real charges are made.

## Solution

| Project | Research area | Documentation |
|---|---|---|
| FeatureFlagApi | ASP.NET Core API using Microsoft.FeatureManagement, with runnable HTTP demonstrations | [Project guide](FeatureFlagApi/README.md) |

The solution targets .NET 11. Start with the [project setup instructions](FeatureFlagApi/README.md#running-the-demonstrations).

## Demonstration catalog

| Demonstration | Research question | Guide | HTTP sample |
|---|---|---|---|
| Global release flag | How do we switch all payments to a new implementation? | [Release flag](FeatureFlagApi/README.md#global-release-flag) | [ReleaseFlag.http](FeatureFlagApi/Requests/ReleaseFlag.http) |
| Customer-specific release | How do we release only to selected customer IDs? | [Customer targeting](FeatureFlagApi/README.md#customer-specific-release) | [CustomerSpecificRelease.http](FeatureFlagApi/Requests/CustomerSpecificRelease.http) |
| Percentage rollout | How do we enable a stable percentage of customers? | [Percentage rollout](FeatureFlagApi/README.md#percentage-rollout) | [PercentageRollout.http](FeatureFlagApi/Requests/PercentageRollout.http) |
| Kill switch | How do we pause refunds manually or after a simulated failure? | [Kill switch](FeatureFlagApi/README.md#refund-kill-switch) | [KillSwitch.http](FeatureFlagApi/Requests/KillSwitch.http) |
| Experiment flag | How do we assign variants and compare their outcomes? | [Experiment](FeatureFlagApi/README.md#payment-retry-experiment) | [ExperimentFlag.http](FeatureFlagApi/Requests/ExperimentFlag.http) |
| Time window | How do we schedule provider availability? | [Time window](FeatureFlagApi/README.md#scheduled-provider-release) | [TimeWindowRelease.http](FeatureFlagApi/Requests/TimeWindowRelease.http) |
| Batch quota | How do we guarantee an exact allocation within a batch? | [Quota routing](FeatureFlagApi/README.md#quota-based-routing) | [QuotaRouting.http](FeatureFlagApi/Requests/QuotaRouting.http) |
| TPS routing | How do we route overflow when provider capacity is exhausted? | [TPS routing](FeatureFlagApi/README.md#tps-based-routing) | [TpsRouting.http](FeatureFlagApi/Requests/TpsRouting.http) |
| Concurrent TPS burst | How does the TPS limit behave under concurrent traffic? | [Concurrent burst](FeatureFlagApi/README.md#concurrent-tps-burst) | [TpsRoutingConcurrent.http](FeatureFlagApi/Requests/TpsRoutingConcurrent.http) |
| Combined filters | How do customer targeting, daily scheduling, and random sampling work together? | [Combined filters](FeatureFlagApi/README.md#combined-filters) | [CombinedFilters.http](FeatureFlagApi/Requests/CombinedFilters.http) |
| Combined filters (OR) | How can any one filter enable a release? | [OR filters](FeatureFlagApi/README.md#combined-filters-with-or-logic) | [AnyFilters.http](FeatureFlagApi/Requests/AnyFilters.http) |

## Documentation structure

The root README is the research catalog: it describes the solution and indexes
available demonstrations. Each project README owns the purpose, implementation,
configuration, and practical examples for that project's features. New projects
should add a project guide and an entry here; new demonstrations should add a
catalog row and details in the relevant project guide.



