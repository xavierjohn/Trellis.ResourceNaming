namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// Resource-by-resource accessors over an <see cref="AzureResourceNamingOptions"/>. Each method bakes in its
/// resource type — the analog of the ASP template's <c>Get*Url</c> / <c>Get*Uri</c> extensions — so the
/// caller never threads a resource type or repeats the deployment context.
/// </summary>
public static class AzureResourceNamingOptionsExtensions
{
    private static readonly IResourceNamer Namer = new AzureResourceNamer();

    // ---- Storage -------------------------------------------------------------------------------

    /// <summary>The storage account name. Omit <paramref name="region"/> for the cloud-singleton (shared) account.</summary>
    public static string StorageName(this AzureResourceNamingOptions context, string? region = null, string? instance = null) =>
        context.Name(AzureResourceTypes.StorageAccount, region, instance);

    /// <summary>The Blob endpoint for a storage account.</summary>
    public static Uri BlobUrl(this AzureResourceNamingOptions context, string? region = null, string? instance = null) =>
        AzureEndpoints.Blob(context.StorageName(region, instance), Endpoints(context));

    /// <summary>The Queue endpoint for a storage account.</summary>
    public static Uri QueueUrl(this AzureResourceNamingOptions context, string? region = null, string? instance = null) =>
        AzureEndpoints.Queue(context.StorageName(region, instance), Endpoints(context));

    /// <summary>The Table endpoint for a storage account.</summary>
    public static Uri TableUrl(this AzureResourceNamingOptions context, string? region = null, string? instance = null) =>
        AzureEndpoints.Table(context.StorageName(region, instance), Endpoints(context));

    // ---- Key Vault (regional) ------------------------------------------------------------------

    /// <summary>The Key Vault name (regional).</summary>
    public static string KeyVaultName(this AzureResourceNamingOptions context) =>
        context.Name(AzureResourceTypes.KeyVault, region: context.Region);

    /// <summary>The Key Vault URI.</summary>
    public static Uri KeyVaultUri(this AzureResourceNamingOptions context) =>
        AzureEndpoints.KeyVault(context.KeyVaultName(), Endpoints(context));

    // ---- Service Bus / Event Hubs — connect alias (cloud-singleton, no region) ------------------
    //
    // A service ALWAYS connects to the stable alias (no region); whether it resolves to a single
    // namespace or a geo-DR pair is an infra detail behind the alias. Provisioning creates the regional
    // PHYSICAL namespaces (primary + secondary) via the *PhysicalNamespaceName methods below — never a
    // physical namespace named as the alias.

    /// <summary>The Service Bus connect-alias name (cloud-singleton, no region).</summary>
    public static string ServiceBusName(this AzureResourceNamingOptions context) =>
        context.Name(AzureResourceTypes.ServiceBusNamespace);

    /// <summary>The Service Bus connect-alias fully-qualified namespace (what <c>ServiceBusClient</c> takes).</summary>
    public static string ServiceBusNamespace(this AzureResourceNamingOptions context) =>
        AzureEndpoints.ServiceBusNamespace(context.ServiceBusName(), Endpoints(context));

    /// <summary>The Event Hubs connect-alias name (cloud-singleton, no region).</summary>
    public static string EventHubsName(this AzureResourceNamingOptions context) =>
        context.Name(AzureResourceTypes.EventHubsNamespace);

    /// <summary>The Event Hubs connect-alias fully-qualified namespace (what the Event Hubs clients take).</summary>
    public static string EventHubsNamespace(this AzureResourceNamingOptions context) =>
        AzureEndpoints.EventHubsNamespace(context.EventHubsName(), Endpoints(context));

    /// <summary>
    /// The regional PHYSICAL Service Bus namespace name (a primary or secondary of a geo-DR pair). For
    /// provisioning/failover only — services connect through <see cref="ServiceBusNamespace"/>. A region is
    /// required: a physical namespace is never region-less (that name is reserved for the alias).
    /// </summary>
    public static string ServiceBusPhysicalNamespaceName(this AzureResourceNamingOptions context, string region) =>
        context.Name(AzureResourceTypes.ServiceBusNamespace, region: Require(region));

    /// <summary>
    /// The regional PHYSICAL Event Hubs namespace name (a primary or secondary of a geo-DR pair). For
    /// provisioning/failover only — services connect through <see cref="EventHubsNamespace"/>. A region is
    /// required: a physical namespace is never region-less (that name is reserved for the alias).
    /// </summary>
    public static string EventHubsPhysicalNamespaceName(this AzureResourceNamingOptions context, string region) =>
        context.Name(AzureResourceTypes.EventHubsNamespace, region: Require(region));

    // ---- Cosmos (cloud-singleton) --------------------------------------------------------------

    /// <summary>The Cosmos DB account name.</summary>
    public static string CosmosName(this AzureResourceNamingOptions context) =>
        context.Name(AzureResourceTypes.CosmosAccount);

    /// <summary>The Cosmos DB account endpoint.</summary>
    public static Uri CosmosUrl(this AzureResourceNamingOptions context) =>
        AzureEndpoints.Cosmos(context.CosmosName(), Endpoints(context));

    // ---- SQL (cloud-singleton server) ----------------------------------------------------------

    /// <summary>The SQL logical server name.</summary>
    public static string SqlServerName(this AzureResourceNamingOptions context) =>
        context.Name(AzureResourceTypes.SqlServer);

    /// <summary>The SQL logical server fully-qualified name (for the connection string).</summary>
    public static string SqlServerFqdn(this AzureResourceNamingOptions context) =>
        AzureEndpoints.SqlServer(context.SqlServerName(), Endpoints(context));

    // ---- Names without a connect endpoint ------------------------------------------------------

    /// <summary>A user-assigned managed identity name (regional).</summary>
    public static string ManagedIdentityName(this AzureResourceNamingOptions context, string? instance = null) =>
        context.Name(AzureResourceTypes.ManagedIdentity, region: context.Region, instance: instance);

    /// <summary>An App Service name (regional).</summary>
    public static string AppServiceName(this AzureResourceNamingOptions context) =>
        context.Name(AzureResourceTypes.AppService, region: context.Region);

    /// <summary>The Container Registry name.</summary>
    public static string ContainerRegistryName(this AzureResourceNamingOptions context) =>
        context.Name(AzureResourceTypes.ContainerRegistry);

    /// <summary>The Log Analytics workspace name (regional).</summary>
    public static string LogAnalyticsName(this AzureResourceNamingOptions context) =>
        context.Name(AzureResourceTypes.LogAnalytics, region: context.Region);

    /// <summary>The resource group name for the service slice (regional).</summary>
    public static string ResourceGroupName(this AzureResourceNamingOptions context) =>
        context.Name(AzureResourceTypes.ResourceGroup, region: context.Region);

    // ---- Escape hatch for any other type -------------------------------------------------------

    /// <summary>Computes a name for any resource type, with optional region/instance overrides.</summary>
    public static string Name(
        this AzureResourceNamingOptions context,
        ResourceTypeSpec type,
        string? region = null,
        string? instance = null) =>
        Namer.Name(Build(context, type, region, instance));

    private static NamingRequest Build(
        AzureResourceNamingOptions context, ResourceTypeSpec type, string? region, string? instance)
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

    private static CloudEndpoints Endpoints(AzureResourceNamingOptions context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return AzureClouds.ByName(context.Cloud);
    }
}
