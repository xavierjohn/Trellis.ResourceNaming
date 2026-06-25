using Xunit;

namespace Trellis.ResourceNaming.Azure.Tests;

public class DeployedEnvironmentOptionsTests
{
    private static readonly DeployedEnvironmentOptions Ctx = new()
    {
        System = "ptk",
        Service = "mbr",
        Environment = "prod",
        Region = "westus3",
        RegionShortName = "weu",
        Cloud = KnownClouds.AzureCloud,
    };

    [Fact]
    public void Blob_url_shared() =>
        Assert.Equal("https://ptkmbrstprod.blob.core.windows.net/", Ctx.BlobUrl().AbsoluteUri);

    [Fact]
    public void Blob_url_regional_checkpoint() =>
        Assert.Equal("https://ptkmbrstprodweu.blob.core.windows.net/",
            Ctx.BlobUrl(region: Ctx.RegionShortName).AbsoluteUri);

    [Fact]
    public void KeyVault_uri_is_regional() =>
        Assert.Equal("https://ptk-mbr-kv-prod-weu.vault.azure.net/", Ctx.KeyVaultUri().AbsoluteUri);

    [Fact]
    public void ServiceBus_namespace() =>
        Assert.Equal("ptk-mbr-sbns-prod.servicebus.windows.net", Ctx.ServiceBusNamespace());

    [Fact]
    public void EventHubs_namespace_is_the_connect_alias() =>
        Assert.Equal("ptk-mbr-evhns-prod.servicebus.windows.net", Ctx.EventHubsNamespace());

    [Fact]
    public void ServiceBus_physical_namespace_is_regional() =>
        Assert.Equal("ptk-mbr-sbns-prod-weu", Ctx.ServiceBusPhysicalNamespaceName("weu"));

    [Fact]
    public void EventHubs_physical_namespace_is_regional() =>
        Assert.Equal("ptk-mbr-evhns-prod-neu", Ctx.EventHubsPhysicalNamespaceName("neu"));

    [Fact]
    public void Physical_namespace_requires_a_region() =>
        Assert.Throws<ArgumentException>(() => Ctx.ServiceBusPhysicalNamespaceName("  "));

    [Fact]
    public void Cosmos_url() =>
        Assert.Equal("https://ptk-mbr-cosmos-prod.documents.azure.com/", Ctx.CosmosUrl().AbsoluteUri);

    [Fact]
    public void Sql_server_fqdn() =>
        Assert.Equal("ptk-mbr-sql-prod.database.windows.net", Ctx.SqlServerFqdn());

    [Fact]
    public void ResourceGroup_name_is_regional() =>
        Assert.Equal("rg-ptk-mbr-prod-weu", Ctx.ResourceGroupName());

    [Fact]
    public void ManagedIdentity_name_with_instance() =>
        Assert.Equal("ptk-mbr-id-prod-weu-001", Ctx.ManagedIdentityName("001"));

    [Fact]
    public void Cloud_drives_the_endpoint_suffix()
    {
        var usGov = new DeployedEnvironmentOptions
        {
            System = "ptk", Service = "mbr", Environment = "prod", RegionShortName = "weu",
            Cloud = KnownClouds.AzureUSGovernment,
        };

        Assert.Equal("https://ptkmbrstprod.blob.core.usgovcloudapi.net/", usGov.BlobUrl().AbsoluteUri);
    }
}
