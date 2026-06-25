namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// The per-service DNS suffixes for one Azure cloud environment. Use <see cref="AzureClouds"/> for the
/// well-known clouds, or construct one directly for a sovereign cloud not covered there.
/// </summary>
/// <param name="StorageSuffix">
/// Storage base suffix (e.g. <c>core.windows.net</c>); the per-service endpoints prepend <c>blob.</c>,
/// <c>queue.</c>, <c>table.</c>, <c>file.</c>, or <c>dfs.</c>.
/// </param>
/// <param name="KeyVaultSuffix">Key Vault suffix, e.g. <c>vault.azure.net</c>.</param>
/// <param name="ServiceBusSuffix">Service Bus / Event Hubs suffix, e.g. <c>servicebus.windows.net</c>.</param>
/// <param name="CosmosSuffix">Cosmos DB suffix, e.g. <c>documents.azure.com</c>.</param>
public sealed record CloudEndpoints(
    string StorageSuffix,
    string KeyVaultSuffix,
    string ServiceBusSuffix,
    string CosmosSuffix);
