namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// Resource-by-resource accessors over a <see cref="DeployedEnvironmentOptions"/>. Each method bakes in its
/// resource type — the analog of the ASP template's <c>Get*Url</c> / <c>Get*Uri</c> extensions — so the
/// caller never threads a resource type or repeats the deployment context. Regional names use the deployment's
/// <see cref="DeployedEnvironmentOptions.RegionShortName"/>.
/// </summary>
public static class DeployedEnvironmentOptionsExtensions
{
    private static readonly IResourceNamer Namer = new AzureResourceNamer();

    // ---- Storage -------------------------------------------------------------------------------

    /// <summary>The storage account name. Omit <paramref name="region"/> for the cloud-singleton (shared) account.</summary>
    public static string StorageName(this DeployedEnvironmentOptions context, string? region = null, string? instance = null) =>
        context.Name(AzureResourceTypes.StorageAccount, region, instance);

    /// <summary>The Blob endpoint for a storage account.</summary>
    public static Uri BlobUrl(this DeployedEnvironmentOptions context, string? region = null, string? instance = null) =>
        AzureEndpoints.Blob(context.StorageName(region, instance), Endpoints(context));

    /// <summary>The Queue endpoint for a storage account.</summary>
    public static Uri QueueUrl(this DeployedEnvironmentOptions context, string? region = null, string? instance = null) =>
        AzureEndpoints.Queue(context.StorageName(region, instance), Endpoints(context));

    /// <summary>The Table endpoint for a storage account.</summary>
    public static Uri TableUrl(this DeployedEnvironmentOptions context, string? region = null, string? instance = null) =>
        AzureEndpoints.Table(context.StorageName(region, instance), Endpoints(context));

    // ---- Key Vault (regional) ------------------------------------------------------------------

    /// <summary>The Key Vault name (regional).</summary>
    public static string KeyVaultName(this DeployedEnvironmentOptions context) =>
        context.Name(AzureResourceTypes.KeyVault, region: context.RegionShortName);

    /// <summary>The Key Vault URI.</summary>
    public static Uri KeyVaultUri(this DeployedEnvironmentOptions context) =>
        AzureEndpoints.KeyVault(context.KeyVaultName(), Endpoints(context));

    // ---- Service Bus / Event Hubs — connect alias (cloud-singleton, no region) ------------------
    //
    // A service ALWAYS connects to the stable alias (no region); whether it resolves to a single
    // namespace or a geo-DR pair is an infra detail behind the alias. Provisioning creates the regional
    // PHYSICAL namespaces (primary + secondary) via the *PhysicalNamespaceName methods below — never a
    // physical namespace named as the alias.

    /// <summary>The Service Bus connect-alias name (cloud-singleton, no region).</summary>
    public static string ServiceBusName(this DeployedEnvironmentOptions context) =>
        context.Name(AzureResourceTypes.ServiceBusNamespace);

    /// <summary>The Service Bus connect-alias fully-qualified namespace (what <c>ServiceBusClient</c> takes).</summary>
    public static string ServiceBusNamespace(this DeployedEnvironmentOptions context) =>
        AzureEndpoints.ServiceBusNamespace(context.ServiceBusName(), Endpoints(context));

    /// <summary>The Event Hubs connect-alias name (cloud-singleton, no region).</summary>
    public static string EventHubsName(this DeployedEnvironmentOptions context) =>
        context.Name(AzureResourceTypes.EventHubsNamespace);

    /// <summary>The Event Hubs connect-alias fully-qualified namespace (what the Event Hubs clients take).</summary>
    public static string EventHubsNamespace(this DeployedEnvironmentOptions context) =>
        AzureEndpoints.EventHubsNamespace(context.EventHubsName(), Endpoints(context));

    /// <summary>
    /// The regional PHYSICAL Service Bus namespace name (a primary or secondary of a geo-DR pair). For
    /// provisioning/failover only — services connect through <see cref="ServiceBusNamespace"/>. A region is
    /// required: a physical namespace is never region-less (that name is reserved for the alias).
    /// </summary>
    public static string ServiceBusPhysicalNamespaceName(this DeployedEnvironmentOptions context, string region) =>
        context.Name(AzureResourceTypes.ServiceBusNamespace, region: Require(region));

    /// <summary>
    /// The regional PHYSICAL Event Hubs namespace name (a primary or secondary of a geo-DR pair). For
    /// provisioning/failover only — services connect through <see cref="EventHubsNamespace"/>. A region is
    /// required: a physical namespace is never region-less (that name is reserved for the alias).
    /// </summary>
    public static string EventHubsPhysicalNamespaceName(this DeployedEnvironmentOptions context, string region) =>
        context.Name(AzureResourceTypes.EventHubsNamespace, region: Require(region));

    // ---- Cosmos (cloud-singleton) --------------------------------------------------------------

    /// <summary>The Cosmos DB account name.</summary>
    public static string CosmosName(this DeployedEnvironmentOptions context) =>
        context.Name(AzureResourceTypes.CosmosAccount);

    /// <summary>The Cosmos DB account endpoint.</summary>
    public static Uri CosmosUrl(this DeployedEnvironmentOptions context) =>
        AzureEndpoints.Cosmos(context.CosmosName(), Endpoints(context));

    // ---- SQL (cloud-singleton server) ----------------------------------------------------------

    /// <summary>The SQL logical server name.</summary>
    public static string SqlServerName(this DeployedEnvironmentOptions context) =>
        context.Name(AzureResourceTypes.SqlServer);

    /// <summary>The SQL logical server fully-qualified name (for the connection string).</summary>
    public static string SqlServerFqdn(this DeployedEnvironmentOptions context) =>
        AzureEndpoints.SqlServer(context.SqlServerName(), Endpoints(context));

    // ---- Names without a connect endpoint ------------------------------------------------------

    /// <summary>A user-assigned managed identity name (regional).</summary>
    public static string ManagedIdentityName(this DeployedEnvironmentOptions context, string? instance = null) =>
        context.Name(AzureResourceTypes.ManagedIdentity, region: context.RegionShortName, instance: instance);

    /// <summary>An App Service name (regional).</summary>
    public static string AppServiceName(this DeployedEnvironmentOptions context) =>
        context.Name(AzureResourceTypes.AppService, region: context.RegionShortName);

    /// <summary>The Container Registry name.</summary>
    public static string ContainerRegistryName(this DeployedEnvironmentOptions context) =>
        context.Name(AzureResourceTypes.ContainerRegistry);

    /// <summary>The Log Analytics workspace name (regional).</summary>
    public static string LogAnalyticsName(this DeployedEnvironmentOptions context) =>
        context.Name(AzureResourceTypes.LogAnalytics, region: context.RegionShortName);

    /// <summary>The resource group name for the service slice (regional).</summary>
    public static string ResourceGroupName(this DeployedEnvironmentOptions context) =>
        context.Name(AzureResourceTypes.ResourceGroup, region: context.RegionShortName);

    // ---- Escape hatch for any other type -------------------------------------------------------

    /// <summary>Computes a name for any resource type, with optional region/instance overrides.</summary>
    public static string Name(
        this DeployedEnvironmentOptions context,
        ResourceTypeSpec type,
        string? region = null,
        string? instance = null) =>
        Namer.Name(Build(context, type, region, instance));

    private static NamingRequest Build(
        DeployedEnvironmentOptions context, ResourceTypeSpec type, string? region, string? instance)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new NamingRequest
        {
            System = context.System,
            Service = context.Service,
            ResourceType = type,
            Environment = context.Environment,
            Region = region,
            Stamp = context.Stamp,
            Instance = instance,
            Cloud = context.Cloud,
            Scope = context.Scope,
        };
    }

    private static string Require(string region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        return region;
    }

    private static CloudEndpoints Endpoints(DeployedEnvironmentOptions context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return AzureClouds.ByName(context.Cloud);
    }
}
