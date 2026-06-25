namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// The identity and per-service DNS suffixes for one Azure cloud environment. Use <see cref="AzureClouds"/>
/// for the well-known clouds, or construct one directly for a sovereign cloud not covered there.
/// </summary>
/// <param name="LocationMoniker">
/// The cloud's short location moniker (e.g. <c>public</c>), used as the cloud segment of an SLI location id.
/// </param>
/// <param name="StorageSuffix">
/// Storage base suffix (e.g. <c>core.windows.net</c>); the per-service endpoints prepend <c>blob.</c>,
/// <c>queue.</c>, <c>table.</c>, <c>file.</c>, or <c>dfs.</c>.
/// </param>
/// <param name="KeyVaultSuffix">Key Vault suffix, e.g. <c>vault.azure.net</c>.</param>
/// <param name="ServiceBusSuffix">Service Bus / Event Hubs suffix, e.g. <c>servicebus.windows.net</c>.</param>
/// <param name="CosmosSuffix">Cosmos DB suffix, e.g. <c>documents.azure.com</c>.</param>
/// <param name="SqlSuffix">SQL logical-server suffix, e.g. <c>database.windows.net</c>.</param>
public sealed record CloudEndpoints(
    string LocationMoniker,
    string StorageSuffix,
    string KeyVaultSuffix,
    string ServiceBusSuffix,
    string CosmosSuffix,
    string SqlSuffix);
