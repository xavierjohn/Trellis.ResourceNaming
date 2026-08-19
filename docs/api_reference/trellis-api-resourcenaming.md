---
package: Trellis.ResourceNaming.Abstractions, Trellis.ResourceNaming.Azure
namespaces: [Trellis.ResourceNaming, Trellis.ResourceNaming.Azure]
types: [IResourceNamer, NamingRequest, ResourceTypeSpec, CloudScope, NameSeparator, NamingPolicy, ResourceNameOverflowException, AzureResourceNamer, AzureResourceTypes, AzureEndpoints, AzureClouds, CloudEndpoints, KnownClouds, DeployedEnvironmentOptions, DeployedEnvironmentOptionsExtensions]
version: v1
last_verified: 2026-08-18
audience: [llm]
---
# Trellis.ResourceNaming — API Reference

- **Packages:** `Trellis.ResourceNaming.Abstractions` (cloud-agnostic contracts), `Trellis.ResourceNaming.Azure` (the Azure convention)
- **Namespaces:** `Trellis.ResourceNaming`, `Trellis.ResourceNaming.Azure`

> **Read this before generating any resource name.** Almost nothing here fails at compile time. Wrong input produces a *different valid name*, and a resource name is a persistent infrastructure identifier — a name that is merely wrong points at a resource that does not exist, or at one belonging to a different slice. The compiler will not save you. This document is organised around the decisions that go wrong, not around the type list.

One doc covers both packages deliberately: a single expression routinely crosses the boundary, using `Trellis.ResourceNaming.Azure`'s catalog together with `Trellis.ResourceNaming`'s `ResourceTypeSpec` to patch a gap in it.

---

## Start here — pick the right call

| You want | Use | Do **not** use |
|---|---|---|
| A name for a type in the catalog | The named accessor, e.g. `context.KeyVaultName()` | `new AzureResourceNamer().Name(new NamingRequest { … })` — it re-threads context you already bound |
| A name for a type **not** in the catalog | `context.Name(new ResourceTypeSpec(…), region: …)` | Inventing an `AzureResourceTypes` member — the catalog is a fixed set of 13 |
| A **region-less** name for a type whose accessor is regional | `context.Name(AzureResourceTypes.X)` (escape hatch, omit `region`) | The named accessor — it forces the region token in |
| A connect URL or host | `context.BlobUrl()`, `context.KeyVaultUri()`, `context.ServiceBusNamespace()`, … | Building the host by string concatenation |
| A name inside Bicep/Terraform | Pass the name **in** as a parameter from C# | Recomputing it in the IaC language — see [The IaC seam](#the-iac-seam) |

If you cannot find your resource type below, you want the escape hatch. That is a supported path, not a workaround.

---

## The five traps

Ranked by how expensive the mistake is, not by how likely you are to hit it. Every output below is actual program output, not an illustration.

### 1. Regional accessors force the region in; the escape hatch is how you opt out

`ResourceGroupName()`, `KeyVaultName()`, `AppServiceName()`, `ManagedIdentityName()` and `LogAnalyticsName()` all read `RegionShortName` off the context and **throw if it is not set**. There is no way to ask them for a region-less name.

So a globally-scoped resource group cannot be obtained from `ResourceGroupName()`:

```csharp
// context: System=ptk, Service=mbr, Environment=prod, RegionShortName=weu

context.ResourceGroupName()                        // rg-ptk-mbr-prod-weu   (regional slice)
context.Name(AzureResourceTypes.ResourceGroup)     // rg-ptk-mbr-prod       (global slice)
```

Both succeed. Both are valid names. Only one is the resource group you meant. **This is the failure with no safety net at all** — reach for the accessor out of habit and you silently address the wrong resource group.

When `RegionShortName` is unset the two diverge more visibly, which is how the mistake usually surfaces:

```csharp
context.ResourceGroupName()                     // throws InvalidOperationException
context.Name(AzureResourceTypes.ResourceGroup)  // rg-ptk-mbr-prod
```

### 2. The uniqueness suffix cannot be reproduced outside C#

`CloudScope.Shared` — **the default**, on both `NamingRequest` and `DeployedEnvironmentOptions` — appends a deterministic five-character suffix to every type marked `IsDnsGlobal`. It is SHA-256 over the canonical identity, folded to base-36, and it is **not reproducible in Bicep, Terraform or ARM**, none of which can compute SHA-256 over that seed.

```csharp
context.StorageName()   // ptkmbrstproduuwsm          ← "ptkmbrst" + "prod" + 5-char hash
context.CosmosName()    // ptk-mbr-cosmos-prod-8lo7y
```

This is the entire reason the C#→IaC seam exists. See [The IaC seam](#the-iac-seam).

The seed includes the **cloud**, so the same workload named for a different cloud gets a different name:

```
AzureCloud        → ptkmbrstproduuwsm
AzureUSGovernment → ptkmbrstprodp3wk8
```

The seed always uses the **full** environment word even when the emitted name falls back to one character (trap 3), so the suffix is stable across that fallback.

### 3. The environment word silently shortens to one character to fit

If a name exceeds its type's `MaxLength`, the namer retries once with the environment collapsed to a single character (`local`→`l`, `test`→`t`, `stage`→`s`, `prod`→`p`) before giving up. **This happens silently, and it happens in ordinary configurations** — the suffix from trap 2 is usually what pushes it over:

```csharp
context.KeyVaultName()    // ptk-mbr-kv-p-weu-dldoa       ← "p", not "prod"
context.AppServiceName()  // ptk-mbr-app-prod-weu-c5j3y   ← full word fits here
```

Two resources in the same deployment can therefore disagree about whether the environment reads `prod` or `p`. Do not pattern-match names with a hardcoded environment word, and do not assume two names share a shape merely because they share a context.

Add a stamp and the same call stops working altogether:

```csharp
// same context, plus Stamp = "001"
context.KeyVaultName()
// ResourceNameOverflowException: 'ptk-mbr-kv-p-weu-001-l2b7w' (26 chars) exceeds the 24-char limit for type 'kv'.
```

That is the design working as intended — it fails rather than truncating a disambiguating token into a collision — but it means **short `System` and `Service` codes are a hard requirement, not a style preference**, and that a config change can break naming for one resource type while leaving every other name working.

### 4. Half the accessors take the region from context; half require you to pass it

There is no single rule; it is per resource type, and the two families read almost identically at the call site.

| Reads `RegionShortName` from context (throws if unset) | Takes `region` as an argument (omit ⇒ no region token) |
|---|---|
| `ResourceGroupName()` | `StorageName(region?, instance?)` |
| `KeyVaultName()` | `ServiceBusPhysicalNamespaceName(region)` — **required** |
| `AppServiceName()` | `EventHubsPhysicalNamespaceName(region)` — **required** |
| `ManagedIdentityName(instance?)` | `Name(type, region?, instance?)` — the escape hatch |
| `LogAnalyticsName()` | |

With `RegionShortName = "weu"` set on the context:

```csharp
context.KeyVaultName()      // ptk-mbr-kv-p-weu-dldoa   ← region applied automatically
context.StorageName()       // ptkmbrstproduuwsm        ← RegionShortName IGNORED, no region token
context.StorageName("weu")  // ptkmbrstprodweuylpgl     ← region applied because you passed it
```

`StorageName()` is a cloud-singleton unless you say otherwise; `KeyVaultName()` is regional and cannot be anything else. Neither reads wrong at a glance, which is what makes this worth checking every time.

The remaining name accessors — `ContainerRegistryName()`, `ServiceBusName()`, `EventHubsName()`, `CosmosName()`, `SqlServerName()` — take no region at all and are always cloud-singletons.

### 5. The cloud is validated when you ask for an endpoint, not when you ask for a name

`Cloud` is opaque to the namer: any non-empty string works, and it merely seeds the hash. `AzureClouds.ByName` is strict and accepts only a `KnownClouds` value.

```csharp
// context.Cloud = "AzureSovereign"
context.KeyVaultName()   // ptk-kv-prod-weu-k2bu6   ← succeeds
context.KeyVaultUri()    // NotSupportedException: Cloud 'AzureSovereign' is not a known Azure cloud.
```

A typo in `Cloud` therefore produces perfectly valid *names* — with a different hash than intended — and fails only later, at the first endpoint call. For a sovereign cloud outside `KnownClouds`, construct a `CloudEndpoints` directly and call the `AzureEndpoints` methods yourself; the `DeployedEnvironmentOptions` URL accessors always route through `AzureClouds.ByName`.

---

## Name shape

```
{system}-{service}-{type}-{env}-{region}-{stamp}-{instance}[-{hash5}]
```

Resource groups are the exception: the type abbreviation becomes a universal **`rg-` prefix** and the instance token is dropped, because the resource group *is* the slice.

```
rg-{system}-{service}-{env}-{region}-{stamp}
```

Rules that apply to every name:

- Tokens are lowercase alphanumeric `[a-z0-9]`. Anything else throws — the convention inserts separators itself.
- `Environment` must be a CAF word: `local`, `test`, `stage`, `prod`. Anything else throws, including `dev`, `qa` and `ppe`.
- Optional tokens (`Service`, `Region`, `Stamp`, `Instance`) are omitted entirely when null, not left blank.
- `NameSeparator.None` types concatenate with no separator; `Dash` types join with `-`.
- The cloud is **never** a name token. It selects the endpoint suffix and seeds the hash.
- Inputs are trimmed and lowercased before validation.

Worked examples with `System=ptk`, `Service=mbr`, `Environment=prod`, `Scope=Isolated` (no hash, so the shape stays visible):

| Call | Name |
|---|---|
| `Name(ResourceGroup)` | `rg-ptk-mbr-prod` |
| `Name(ResourceGroup, region: "weu")` | `rg-ptk-mbr-prod-weu` |
| `StorageName()` | `ptkmbrstprod` |
| `KeyVaultName()` (context `RegionShortName=weu`) | `ptk-mbr-kv-prod-weu` |
| `ManagedIdentityName()` | `ptk-mbr-id-prod-weu` |
| `Name(SqlDatabase)` | `ptk-mbr-sqldb-prod` |

---

## `DeployedEnvironmentOptions`

Bind once from configuration; every name and URL derives from it.

| Property | Type | Default | Notes |
|---|---|---|---|
| `System` | `string` | `""` | Product / platform short code. Required — blank throws. |
| `Service` | `string?` | `null` | Bounded-context code. Omit for system-shared resources. |
| `Environment` | `string` | `""` | CAF word only. |
| `Region` | `string?` | `null` | Full region name (`westeurope`). **Display and telemetry only — never a name token.** |
| `RegionShortName` | `string?` | `null` | The region token (`weu`) used by regional accessors. |
| `Cloud` | `string` | `KnownClouds.AzureCloud` | Selects endpoint suffixes; seeds the hash. |
| `Scope` | `CloudScope` | `Shared` | `Shared` adds the uniqueness suffix to DNS-global types. |
| `Stamp` | `string?` | `null` | Scale-unit ordinal; applies to every name derived from this context. |

`Region` and `RegionShortName` are independent fields with no validation tying them together. Set only `Region` and every regional accessor throws; set only `RegionShortName` and telemetry loses the full region. Set both or neither.

Under the default `Scope = Shared`, every DNS-global name carries the five-character suffix. Use `Isolated` only for air-gapped, sovereign or single-tenant clouds that own their DNS namespace.

---

## Resource type catalog

`AzureResourceTypes` — 13 entries. `Sep` is the token separator; `DNS` marks the types that receive the uniqueness suffix under `Shared`.

| Member | Abbr | Min | Max | Sep | DNS | Accessor |
|---|---|---|---|---|---|---|
| `StorageAccount` | `st` | 3 | 24 | None | ✅ | `StorageName(region?, instance?)` |
| `KeyVault` | `kv` | 3 | 24 | Dash | ✅ | `KeyVaultName()` |
| `ServiceBusNamespace` | `sbns` | 6 | 50 | Dash | ✅ | `ServiceBusName()`, `ServiceBusPhysicalNamespaceName(region)` |
| `EventHubsNamespace` | `evhns` | 6 | 50 | Dash | ✅ | `EventHubsName()`, `EventHubsPhysicalNamespaceName(region)` |
| `CosmosAccount` | `cosmos` | 3 | 44 | Dash | ✅ | `CosmosName()` |
| `SqlServer` | `sql` | 1 | 63 | Dash | ✅ | `SqlServerName()` |
| `SqlDatabase` | `sqldb` | 1 | 128 | Dash | — | *(escape hatch)* |
| `AppService` | `app` | 2 | 60 | Dash | ✅ | `AppServiceName()` |
| `ContainerRegistry` | `cr` | 5 | 50 | None | ✅ | `ContainerRegistryName()` |
| `ManagedIdentity` | `id` | 3 | 128 | Dash | — | `ManagedIdentityName(instance?)` |
| `LogAnalytics` | `log` | 4 | 63 | Dash | — | `LogAnalyticsName()` |
| `ApplicationInsights` | `appi` | 1 | 260 | Dash | — | *(escape hatch)* |
| `ResourceGroup` | `rg` | 1 | 90 | Dash | — | `ResourceGroupName()` |

`SqlDatabase` and `ApplicationInsights` are in the catalog but have **no named accessor** — reach them through `context.Name(AzureResourceTypes.SqlDatabase)`. That is expected, not an oversight.

### Types not in the catalog

Construct a `ResourceTypeSpec` inline. This is the supported extension point:

```csharp
// App Service plans are not in the shipped catalog.
var plan = new ResourceTypeSpec("plan", MinLength: 1, MaxLength: 40,
                                NameSeparator.Dash, IsDnsGlobal: false);

context.Name(plan, region: "weu");   // ptk-mbr-plan-prod-weu
```

Set `IsDnsGlobal: true` only if the platform requires the name to be globally unique — it is what triggers the suffix, and enabling it for a resource-group-scoped type produces a needlessly cryptic name. Cap `MaxLength` at the *platform* limit or lower; the App Service plan cap above is deliberately 40, well below the platform's own, to keep names readable.

---

## Connect endpoints

`AzureEndpoints` takes a name plus a `CloudEndpoints`; the `DeployedEnvironmentOptions` accessors resolve `CloudEndpoints` from `Cloud` automatically. Note which return `Uri` and which return a bare host `string` — the messaging and SQL clients want a host, not a URL.

| Accessor | `AzureEndpoints` equivalent | Returns | Example (public cloud) |
|---|---|---|---|
| `BlobUrl(region?, instance?)` | `Blob(name, cloud)` | `Uri` | `https://ptkmbrstproduuwsm.blob.core.windows.net/` |
| `QueueUrl(region?, instance?)` | `Queue(name, cloud)` | `Uri` | `https://{name}.queue.core.windows.net/` |
| `TableUrl(region?, instance?)` | `Table(name, cloud)` | `Uri` | `https://{name}.table.core.windows.net/` |
| *(none)* | `File(name, cloud)` | `Uri` | `https://{name}.file.core.windows.net/` |
| *(none)* | `DataLake(name, cloud)` | `Uri` | `https://{name}.dfs.core.windows.net/` |
| `KeyVaultUri()` | `KeyVault(name, cloud)` | `Uri` | `https://ptk-mbr-kv-p-weu-dldoa.vault.azure.net/` |
| `ServiceBusNamespace()` | `ServiceBusNamespace(name, cloud)` | `string` | `ptk-mbr-sbns-prod-ft6cp.servicebus.windows.net` |
| `EventHubsNamespace()` | `EventHubsNamespace(name, cloud)` | `string` | `{name}.servicebus.windows.net` |
| `CosmosUrl()` | `Cosmos(name, cloud)` | `Uri` | `https://ptk-mbr-cosmos-prod-8lo7y.documents.azure.com/` |
| `SqlServerFqdn()` | `SqlServer(name, cloud)` | `string` | `ptk-mbr-sql-prod-2v68g.database.windows.net` |

`File` and `DataLake` exist on `AzureEndpoints` with no `DeployedEnvironmentOptions` accessor — call them with `context.StorageName(…)` and a `CloudEndpoints`. `EventHubsNamespace` delegates to `ServiceBusNamespace`: Event Hubs shares the Service Bus DNS domain.

### Cloud suffixes

`AzureClouds.ByName(string)` maps a `KnownClouds` value to `CloudEndpoints` and throws `NotSupportedException` otherwise. The three sets are `AzureClouds.Public`, `.UsGovernment` and `.China`.

| | `AzureCloud` | `AzureUSGovernment` | `AzureChinaCloud` |
|---|---|---|---|
| `StorageSuffix` | `core.windows.net` | `core.usgovcloudapi.net` | `core.chinacloudapi.cn` |
| `KeyVaultSuffix` | `vault.azure.net` | `vault.usgovcloudapi.net` | `vault.azure.cn` |
| `ServiceBusSuffix` | `servicebus.windows.net` | `servicebus.usgovcloudapi.net` | `servicebus.chinacloudapi.cn` |
| `CosmosSuffix` | `documents.azure.com` | `documents.azure.us` | `documents.azure.cn` |
| `SqlSuffix` | `database.windows.net` | `database.usgovcloudapi.net` | `database.chinacloudapi.cn` |

The suffixes are **not** a uniform substitution. US Government uses `documents.azure.us` for Cosmos while everything else takes `usgovcloudapi.net`; China uses `vault.azure.cn` and `documents.azure.cn` while storage and Service Bus take `chinacloudapi.cn`. Never derive one suffix from another.

### Service Bus and Event Hubs: alias vs physical namespace

A service **always** connects to the region-less alias. The regional physical namespaces exist for provisioning and failover only.

```csharp
context.ServiceBusName()                          // ptk-mbr-sbns-prod-ft6cp       ← connect alias
context.ServiceBusPhysicalNamespaceName("weu")    // ptk-mbr-sbns-prod-weu-crl6l   ← geo-DR member
```

Whether the alias resolves to one namespace or to a geo-DR pair is an infrastructure detail behind it. Never provision a physical namespace under the alias name, and never point a service at a physical namespace name.

---

## The IaC seam

The five-character suffix from trap 2 is SHA-256 based and **cannot be recomputed in Bicep, ARM or Terraform**. This is a deliberate boundary, not a gap to be closed.

**Compute names in C# and pass them into the IaC layer as parameters.** Do not attempt to reproduce the algorithm in the IaC language — an approximation drifts from what the application resolves at runtime, and because the two only diverge for DNS-global types, the mistake surfaces as a handful of unreachable resources rather than as an obvious failure.

If asked to add a resource to a Bicep template, add a parameter and supply it from the naming code. If asked to make Bicep compute the name, explain why that cannot work.

---

## Failure modes

| Condition | Exception | Notes |
|---|---|---|
| Token contains anything outside `[a-z0-9]` | `ArgumentException` | Parameter name identifies the offending token |
| `Environment` is not a CAF word | `ArgumentException` | `dev`, `qa`, `ppe` all throw |
| `System`, `Environment` or `Cloud` null or blank | `ArgumentException` | Checked before any assembly |
| `ResourceType` or `request` null | `ArgumentNullException` | |
| Name exceeds `MaxLength` after env fallback | `ResourceNameOverflowException` | Never truncates |
| Name below `MinLength` | `ArgumentException` | Lengthen the system/service codes |
| Regional accessor with no `RegionShortName` | `InvalidOperationException` | Only from the context-reading family |
| `Cloud` not a `KnownClouds` value, endpoint requested | `NotSupportedException` | Naming still succeeds — see trap 5 |

All validation throws. Nothing here returns a `Result` or a `Maybe`; this library is deliberately outside the Trellis railway, because a bad resource name is a deployment-time programming error rather than a runtime domain outcome.

---

## Policy versioning

`NamingPolicy.Version` (currently `"1.0"`) identifies the naming-policy contract. Names are persistent infrastructure identifiers, so a change in how they are computed is a versioned, opt-in event. If a change would alter an existing name, that is a policy-version change and a migration — not a bug fix.

---

## Package boundary

| Package | Contains | Depends on |
|---|---|---|
| `Trellis.ResourceNaming.Abstractions` | `IResourceNamer`, `NamingRequest`, `ResourceTypeSpec`, `CloudScope`, `NameSeparator`, `NamingPolicy`, `ResourceNameOverflowException` | nothing |
| `Trellis.ResourceNaming.Azure` | `AzureResourceNamer`, `AzureResourceTypes`, `AzureEndpoints`, `AzureClouds`, `CloudEndpoints`, `KnownClouds`, `DeployedEnvironmentOptions` and its extensions | `Trellis.ResourceNaming.Abstractions` |

Neither package depends on `Trellis.Core`, and neither participates in the Trellis framework's lockstep versioning — they version independently.

`AzureResourceNamer` is stateless and thread-safe; `DeployedEnvironmentOptionsExtensions` holds a single static instance. There is no DI registration extension — bind `DeployedEnvironmentOptions` from configuration and call the extension methods on it.
