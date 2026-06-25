namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// Resource-by-resource accessors over an <see cref="AzureResourceContext"/>. Each method bakes in its
/// resource type — the analog of the ASP template's <c>Get*Url</c> / <c>Get*Uri</c> extensions — so the
/// caller never threads a resource type or repeats the deployment context.
/// </summary>
public static class AzureResourceContextExtensions
{
    private static readonly IResourceNamer Namer = new AzureResourceNamer();

    // ---- Storage -------------------------------------------------------------------------------

    /// <summary>The storage account name. Omit <paramref name="region"/> for the cloud-singleton (shared) account.</summary>
    public static string StorageName(this AzureResourceContext context, string? role = null, string? region = null) =>
        context.Name(AzureResourceTypes.StorageAccount, role, region);

    /// <summary>The Blob endpoint for a storage account.</summary>
    public static Uri BlobUrl(this AzureResourceContext context, string? role = null, string? region = null) =>
        AzureEndpoints.Blob(context.StorageName(role, region), Endpoints(context));

    /// <summary>The Queue endpoint for a storage account.</summary>
    public static Uri QueueUrl(this AzureResourceContext context, string? role = null, string? region = null) =>
        AzureEndpoints.Queue(context.StorageName(role, region), Endpoints(context));

    /// <summary>The Table endpoint for a storage account.</summary>
    public static Uri TableUrl(this AzureResourceContext context, string? role = null, string? region = null) =>
        AzureEndpoints.Table(context.StorageName(role, region), Endpoints(context));

    // ---- Key Vault (regional) ------------------------------------------------------------------

    /// <summary>The Key Vault name (regional).</summary>
    public static string KeyVaultName(this AzureResourceContext context) =>
        context.Name(AzureResourceTypes.KeyVault, region: context.Region);

    /// <summary>The Key Vault URI.</summary>
    public static Uri KeyVaultUri(this AzureResourceContext context) =>
        AzureEndpoints.KeyVault(context.KeyVaultName(), Endpoints(context));

    // ---- Service Bus (cloud-singleton connect alias) -------------------------------------------

    /// <summary>The Service Bus namespace name.</summary>
    public static string ServiceBusName(this AzureResourceContext context) =>
        context.Name(AzureResourceTypes.ServiceBusNamespace);

    /// <summary>The Service Bus fully-qualified namespace (what <c>ServiceBusClient</c> takes).</summary>
    public static string ServiceBusNamespace(this AzureResourceContext context) =>
        AzureEndpoints.ServiceBusNamespace(context.ServiceBusName(), Endpoints(context));

    // ---- Event Hubs (regional) -----------------------------------------------------------------

    /// <summary>The Event Hubs namespace name (regional).</summary>
    public static string EventHubsName(this AzureResourceContext context) =>
        context.Name(AzureResourceTypes.EventHubsNamespace, region: context.Region);

    /// <summary>The Event Hubs fully-qualified namespace.</summary>
    public static string EventHubsNamespace(this AzureResourceContext context) =>
        AzureEndpoints.EventHubsNamespace(context.EventHubsName(), Endpoints(context));

    // ---- Cosmos (cloud-singleton) --------------------------------------------------------------

    /// <summary>The Cosmos DB account name.</summary>
    public static string CosmosName(this AzureResourceContext context) =>
        context.Name(AzureResourceTypes.CosmosAccount);

    /// <summary>The Cosmos DB account endpoint.</summary>
    public static Uri CosmosUrl(this AzureResourceContext context) =>
        AzureEndpoints.Cosmos(context.CosmosName(), Endpoints(context));

    // ---- SQL (cloud-singleton server) ----------------------------------------------------------

    /// <summary>The SQL logical server name.</summary>
    public static string SqlServerName(this AzureResourceContext context) =>
        context.Name(AzureResourceTypes.SqlServer);

    /// <summary>The SQL logical server fully-qualified name (for the connection string).</summary>
    public static string SqlServerFqdn(this AzureResourceContext context) =>
        AzureEndpoints.SqlServer(context.SqlServerName(), Endpoints(context));

    // ---- Names without a connect endpoint ------------------------------------------------------

    /// <summary>A user-assigned managed identity name (regional).</summary>
    public static string ManagedIdentityName(this AzureResourceContext context, string? instance = null) =>
        context.Name(AzureResourceTypes.ManagedIdentity, region: context.Region, instance: instance);

    /// <summary>An App Service name (regional).</summary>
    public static string AppServiceName(this AzureResourceContext context) =>
        context.Name(AzureResourceTypes.AppService, region: context.Region);

    /// <summary>The Container Registry name.</summary>
    public static string ContainerRegistryName(this AzureResourceContext context) =>
        context.Name(AzureResourceTypes.ContainerRegistry);

    /// <summary>The Log Analytics workspace name (regional).</summary>
    public static string LogAnalyticsName(this AzureResourceContext context) =>
        context.Name(AzureResourceTypes.LogAnalytics, region: context.Region);

    /// <summary>The resource group name for the service slice (regional).</summary>
    public static string ResourceGroupName(this AzureResourceContext context) =>
        context.Name(AzureResourceTypes.ResourceGroup, region: context.Region);

    // ---- Escape hatch for any other type -------------------------------------------------------

    /// <summary>Computes a name for any resource type, with optional role/region/instance overrides.</summary>
    public static string Name(
        this AzureResourceContext context,
        ResourceTypeSpec type,
        string? role = null,
        string? region = null,
        string? instance = null) =>
        Namer.Name(Build(context, type, role, region, instance)).Name;

    /// <summary>Computes the name and governance tags for any resource type.</summary>
    public static NamingResult Describe(
        this AzureResourceContext context,
        ResourceTypeSpec type,
        string? role = null,
        string? region = null,
        string? instance = null) =>
        Namer.Name(Build(context, type, role, region, instance));

    private static NamingRequest Build(
        AzureResourceContext context, ResourceTypeSpec type, string? role, string? region, string? instance)
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
            Role = role,
        };
    }

    private static CloudEndpoints Endpoints(AzureResourceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return AzureClouds.ByName(context.Cloud);
    }
}
