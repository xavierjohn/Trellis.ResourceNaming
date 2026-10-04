# Trellis.ResourceNaming.Azure

Deterministic, [CAF](https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-naming)-aligned
naming **and** endpoint resolution for Azure resources, behind the `IResourceNamer` seam. Bind your
deployed-environment settings once, then ask for names and connect endpoints *by resource* — the cloud,
region, and environment are never repeated per call.

## Install

```bash
dotnet add package Trellis.ResourceNaming.Azure
```

## Quick start

`DeployedEnvironmentOptions` is the deployed-environment context for a service. Set it once; ask by resource:

```csharp
using Trellis.ResourceNaming;        // CloudScope
using Trellis.ResourceNaming.Azure;

var env = new DeployedEnvironmentOptions
{
    System = "ptk",
    Service = "mbr",
    Environment = "prod",
    Region = "westeurope",         // full region name — for display / location
    RegionShortName = "weu",       // short code — the region token used in regional names
    Cloud = KnownClouds.AzureCloud,
    Scope = CloudScope.Isolated,   // pinned for readable output here; the DEFAULT is Shared (see below)
};

env.StorageName();          // ptkmbrstprod
env.BlobUrl();              // https://ptkmbrstprod.blob.core.windows.net/
env.KeyVaultUri();          // https://ptk-mbr-kv-prod-weu.vault.azure.net/
env.ServiceBusNamespace();  // ptk-mbr-sbns-prod.servicebus.windows.net
env.CosmosUrl();            // https://ptk-mbr-cosmos-prod.documents.azure.com/
```

`Scope` defaults to `CloudScope.Shared` — the safe default for commercial/public Azure, whose shared DNS
namespace requires globally-unique names. In `Shared`, globally-DNS-scoped types (Storage, Key Vault, ACR,
Service Bus/Event Hubs, Cosmos, SQL) get a deterministic 5-char `{u5}` suffix (so `StorageName()` becomes
`ptkmbrstprod{u5}`). Set `CloudScope.Isolated` for air-gapped / sovereign / single-tenant clouds that own
their DNS namespace — the example above pins it so the names read cleanly.

Change `Cloud` to another `KnownClouds` value (e.g. `AzureUSGovernment`) and every endpoint switches its DNS
suffix automatically. In `Isolated` the resource *name* is unchanged; in the default `Shared` scope the cloud
also seeds the `{u5}` suffix, so DNS-global names differ per cloud.

## Bind from configuration

```jsonc
// appsettings.json
"DeployedEnvironment": {
  "System": "ptk",
  "Service": "mbr",
  "Environment": "prod",
  "Region": "westeurope",
  "RegionShortName": "weu",
  "Cloud": "AzureCloud"
}
```

```csharp
builder.Services.Configure<DeployedEnvironmentOptions>(
    builder.Configuration.GetSection("DeployedEnvironment"));

// then inject IOptions<DeployedEnvironmentOptions> and call env.BlobUrl(), env.KeyVaultUri(), ...
```

## What you get

| Accessor | Returns |
|---|---|
| `StorageName(region?, instance?)`, `BlobUrl`, `QueueUrl`, `TableUrl` | Storage account name + service endpoints |
| `KeyVaultName()`, `KeyVaultUri()` | Key Vault name + URI (regional) |
| `ServiceBusName()`, `ServiceBusNamespace()` | Service Bus connect-alias name + FQDN |
| `EventHubsName()`, `EventHubsNamespace()` | Event Hubs connect-alias name + FQDN |
| `CosmosName()`, `CosmosUrl()` | Cosmos DB account name + endpoint |
| `SqlServerName()`, `SqlServerFqdn()` | SQL logical server name + host |
| `ManagedIdentityName()`, `AppServiceName()`, `ContainerRegistryName()`, `LogAnalyticsName()`, `ResourceGroupName()` | Other resource names |
| `Name(type, region?, instance?)` | Escape hatch for any `AzureResourceTypes` entry |

Names follow the workload-first pattern `{system}-{service}-{type}-{env}[-{region}][-{stamp}][-{instance}]`,
with condensed (dashless) names for Storage/ACR and a deterministic 5-char uniqueness suffix for
globally-DNS-scoped types in `CloudScope.Shared`. Inputs are validated (lowercase-alphanumeric tokens, a CAF
environment word) and the resolver **fails rather than truncating** a name that won't fit its length budget.

See the full convention:
**[resource-naming.md](https://github.com/xavierjohn/Trellis.Templates/blob/main/shared/conventions/resource-naming.md)**.

## Lower-level building blocks

Most callers only need `DeployedEnvironmentOptions`. Underneath:

- **`IResourceNamer` / `AzureResourceNamer`** — `Name(NamingRequest)` computes one name.
- **`AzureEndpoints`** — builds a connect endpoint from a bare name + a `CloudEndpoints`
  (e.g. `AzureEndpoints.Blob(name, AzureClouds.UsGovernment)`). Secondary to the accessors above; useful for
  a name you already have, or a cloud outside the four built-ins.
- **`AzureClouds` / `KnownClouds`** — the built-in cloud catalog (Public, US Gov, China) and their
  identifiers.

## AI-native

The API reference for coding agents is published by `Trellis.ResourceNaming.Abstractions`, which this
package depends on, and it covers both packages, so one approval is enough. Restoring a package never
installs agent instructions. To opt in, restore your consuming project or solution, then run from its
Git root:

```bash
dotnet new tool-manifest --output .config
dotnet tool install Trellis.AgentDocs --version 0.1.0-preview.20 --tool-manifest .config/dotnet-tools.json
dotnet tool run agentdocs init <solution-or-project>
```

If the repository already has `.config/dotnet-tools.json`, reuse it instead of creating another manifest.
`init` lists `Trellis.ResourceNaming.Abstractions` as pending and prints the package IDs to add to
`approvedPackages` in `.agentdocs/policy.json`. Add it, then run `dotnet tool run agentdocs sync` to install
the reference under Git-root `.agentdocs/`. The reference is on demand: the generated index describes it,
and an agent opens it when its task concerns generating or changing resource names. After a package
upgrade, run `dotnet restore` and then `dotnet tool run agentdocs sync`.
