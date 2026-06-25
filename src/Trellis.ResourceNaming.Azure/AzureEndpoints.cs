namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// Builds connect endpoints for named Azure resources, given the target cloud's DNS suffixes. These are the
/// analog of the ASP template's <c>Get*Url</c> / <c>Get*Uri</c> extension methods: pass a name produced by
/// <see cref="AzureResourceNamer"/> and a <see cref="CloudEndpoints"/> (e.g. from <see cref="AzureClouds"/>).
/// </summary>
public static class AzureEndpoints
{
    /// <summary>The Blob service endpoint for a storage account, e.g. <c>https://{name}.blob.core.windows.net/</c>.</summary>
    public static Uri Blob(string storageAccountName, CloudEndpoints cloud) =>
        StorageService(storageAccountName, "blob", cloud);

    /// <summary>The Queue service endpoint for a storage account.</summary>
    public static Uri Queue(string storageAccountName, CloudEndpoints cloud) =>
        StorageService(storageAccountName, "queue", cloud);

    /// <summary>The Table service endpoint for a storage account.</summary>
    public static Uri Table(string storageAccountName, CloudEndpoints cloud) =>
        StorageService(storageAccountName, "table", cloud);

    /// <summary>The File service endpoint for a storage account.</summary>
    public static Uri File(string storageAccountName, CloudEndpoints cloud) =>
        StorageService(storageAccountName, "file", cloud);

    /// <summary>The Data Lake (dfs) service endpoint for a storage account.</summary>
    public static Uri DataLake(string storageAccountName, CloudEndpoints cloud) =>
        StorageService(storageAccountName, "dfs", cloud);

    /// <summary>The vault URI for a Key Vault, e.g. <c>https://{name}.vault.azure.net/</c>.</summary>
    public static Uri KeyVault(string vaultName, CloudEndpoints cloud)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultName);
        ArgumentNullException.ThrowIfNull(cloud);
        return new Uri($"https://{vaultName}.{cloud.KeyVaultSuffix}/");
    }

    /// <summary>
    /// The fully-qualified namespace for a Service Bus or Event Hubs namespace, e.g.
    /// <c>{name}.servicebus.windows.net</c> (a host, not a URL — what the messaging clients expect).
    /// </summary>
    public static string ServiceBusNamespace(string namespaceName, CloudEndpoints cloud)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);
        ArgumentNullException.ThrowIfNull(cloud);
        return $"{namespaceName}.{cloud.ServiceBusSuffix}";
    }

    /// <summary>The account endpoint for a Cosmos DB account, e.g. <c>https://{name}.documents.azure.com/</c>.</summary>
    public static Uri Cosmos(string accountName, CloudEndpoints cloud)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentNullException.ThrowIfNull(cloud);
        return new Uri($"https://{accountName}.{cloud.CosmosSuffix}/");
    }

    /// <summary>
    /// The fully-qualified name for a SQL logical server, e.g. <c>{name}.database.windows.net</c> — the host
    /// used in the connection string's <c>Server=tcp:…,1433</c>.
    /// </summary>
    public static string SqlServer(string serverName, CloudEndpoints cloud)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentNullException.ThrowIfNull(cloud);
        return $"{serverName}.{cloud.SqlSuffix}";
    }

    /// <summary>
    /// The fully-qualified namespace for an Event Hubs namespace, e.g. <c>{name}.servicebus.windows.net</c>.
    /// Event Hubs shares the Service Bus domain, so this resolves to the same suffix as
    /// <see cref="ServiceBusNamespace"/>.
    /// </summary>
    public static string EventHubsNamespace(string namespaceName, CloudEndpoints cloud) =>
        ServiceBusNamespace(namespaceName, cloud);

    private static Uri StorageService(string account, string service, CloudEndpoints cloud)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentNullException.ThrowIfNull(cloud);
        return new Uri($"https://{account}.{service}.{cloud.StorageSuffix}");
    }
}
