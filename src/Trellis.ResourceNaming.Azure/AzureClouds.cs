namespace Trellis.ResourceNaming.Azure;

/// <summary>The DNS-suffix sets for the well-known Azure cloud environments.</summary>
public static class AzureClouds
{
    /// <summary>Azure public (commercial) cloud endpoints.</summary>
    public static readonly CloudEndpoints Public = new(
        StorageSuffix: "core.windows.net",
        KeyVaultSuffix: "vault.azure.net",
        ServiceBusSuffix: "servicebus.windows.net",
        CosmosSuffix: "documents.azure.com",
        SqlSuffix: "database.windows.net");

    /// <summary>Azure US Government cloud endpoints.</summary>
    public static readonly CloudEndpoints UsGovernment = new(
        StorageSuffix: "core.usgovcloudapi.net",
        KeyVaultSuffix: "vault.usgovcloudapi.net",
        ServiceBusSuffix: "servicebus.usgovcloudapi.net",
        CosmosSuffix: "documents.azure.us",
        SqlSuffix: "database.usgovcloudapi.net");

    /// <summary>Azure China (21Vianet) cloud endpoints.</summary>
    public static readonly CloudEndpoints China = new(
        StorageSuffix: "core.chinacloudapi.cn",
        KeyVaultSuffix: "vault.azure.cn",
        ServiceBusSuffix: "servicebus.chinacloudapi.cn",
        CosmosSuffix: "documents.azure.cn",
        SqlSuffix: "database.chinacloudapi.cn");

    /// <summary>Resolves the endpoints for a <see cref="KnownClouds"/> identifier.</summary>
    /// <param name="cloud">A <see cref="KnownClouds"/> value.</param>
    /// <returns>The matching <see cref="CloudEndpoints"/>.</returns>
    /// <exception cref="NotSupportedException">The identifier is not a known Azure cloud.</exception>
    public static CloudEndpoints ByName(string cloud) => cloud switch
    {
        KnownClouds.AzureCloud => Public,
        KnownClouds.AzureUSGovernment => UsGovernment,
        KnownClouds.AzureChinaCloud => China,
        _ => throw new NotSupportedException(
            $"Cloud '{cloud}' is not a known Azure cloud. Pass a CloudEndpoints directly for sovereign clouds."),
    };
}
