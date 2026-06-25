using Xunit;

namespace Trellis.ResourceNaming.Azure.Tests;

public class AzureEndpointsTests
{
    [Fact]
    public void Blob_public_cloud() =>
        Assert.Equal("https://ptkmbrstprodweu.blob.core.windows.net/",
            AzureEndpoints.Blob("ptkmbrstprodweu", AzureClouds.Public).AbsoluteUri);

    [Fact]
    public void Blob_us_government() =>
        Assert.Equal("https://ptkmbrstprodweu.blob.core.usgovcloudapi.net/",
            AzureEndpoints.Blob("ptkmbrstprodweu", AzureClouds.UsGovernment).AbsoluteUri);

    [Fact]
    public void Queue_public_cloud() =>
        Assert.Equal("https://ptkmbrstprod.queue.core.windows.net/",
            AzureEndpoints.Queue("ptkmbrstprod", AzureClouds.Public).AbsoluteUri);

    [Fact]
    public void KeyVault_public_cloud() =>
        Assert.Equal("https://ptk-mbr-kv-prod-weu.vault.azure.net/",
            AzureEndpoints.KeyVault("ptk-mbr-kv-prod-weu", AzureClouds.Public).AbsoluteUri);

    [Fact]
    public void KeyVault_china() =>
        Assert.Equal("https://ptk-mbr-kv-prod-weu.vault.azure.cn/",
            AzureEndpoints.KeyVault("ptk-mbr-kv-prod-weu", AzureClouds.China).AbsoluteUri);

    [Fact]
    public void ServiceBus_namespace_public_cloud() =>
        Assert.Equal("ptk-sbns-prod.servicebus.windows.net",
            AzureEndpoints.ServiceBusNamespace("ptk-sbns-prod", AzureClouds.Public));

    [Fact]
    public void ServiceBus_namespace_china() =>
        Assert.Equal("ptk-sbns-prod.servicebus.chinacloudapi.cn",
            AzureEndpoints.ServiceBusNamespace("ptk-sbns-prod", AzureClouds.China));

    [Fact]
    public void Cosmos_public_cloud() =>
        Assert.Equal("https://ptk-cosmos-prod.documents.azure.com/",
            AzureEndpoints.Cosmos("ptk-cosmos-prod", AzureClouds.Public).AbsoluteUri);

    [Fact]
    public void ByName_resolves_known_clouds() =>
        Assert.Same(AzureClouds.UsGovernment, AzureClouds.ByName(KnownClouds.AzureUSGovernment));

    [Fact]
    public void ByName_unknown_cloud_throws() =>
        Assert.Throws<NotSupportedException>(() => AzureClouds.ByName("nope"));

    [Fact]
    public void Endpoint_requires_a_name() =>
        Assert.Throws<ArgumentException>(() => AzureEndpoints.Blob("  ", AzureClouds.Public));

    [Fact]
    public void Name_then_endpoint_end_to_end()
    {
        var namer = new AzureResourceNamer();

        var name = namer.Name(new NamingRequest
        {
            System = "ptk",
            Service = "mbr",
            ResourceType = AzureResourceTypes.StorageAccount,
            Environment = "prod",
            Region = "weu",
            Cloud = "us",
            Role = "ehcheckpoint",
        }).Name;

        var url = AzureEndpoints.Blob(name, AzureClouds.Public).AbsoluteUri;

        Assert.Equal("https://ptkmbrstprodweu.blob.core.windows.net/", url);
    }
}
