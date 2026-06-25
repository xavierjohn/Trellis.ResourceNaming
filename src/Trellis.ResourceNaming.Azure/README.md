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
using Trellis.ResourceNaming.Azure;

var env = new DeployedEnvironmentOptions
{
    System = "ptk",
    Service = "mbr",
    Environment = "prod",
    Region = "westus3",            // full region name — for display / location
    RegionShortName = "weu",       // short code — the region token used in regional names
    Cloud = KnownClouds.AzureCloud,
};

env.StorageName();          // ptkmbrstprod
env.BlobUrl();              // https://ptkmbrstprod.blob.core.windows.net/
env.KeyVaultUri();          // https://ptk-mbr-kv-prod-weu.vault.azure.net/
env.ServiceBusNamespace();  // ptk-mbr-sbns-prod.servicebus.windows.net
env.CosmosUrl();            // https://ptk-mbr-cosmos-prod.documents.azure.com/
```

Change `Cloud` to another `KnownClouds` value (e.g. `AzureUSGovernment`) and every endpoint switches its
DNS suffix automatically — the resource *name* is unchanged.

## Bind from configuration

```jsonc
// appsettings.json
"DeployedEnvironment": {
  "System": "ptk",
  "Service": "mbr",
  "Environment": "prod",
  "Region": "westus3",
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
- **`AzureClouds` / `KnownClouds`** — the built-in cloud catalog (Public, US Gov, China, Germany) and their
  identifiers.
