using Xunit;

namespace Trellis.ResourceNaming.Azure.Tests;

public class AzureResourceNamerTests
{
    private static readonly AzureResourceNamer Namer = new();

    private static NamingRequest Request(
        ResourceTypeSpec type,
        string system,
        string? service = null,
        string env = "prod",
        string? region = null,
        string? stamp = null,
        string? instance = null,
        CloudScope scope = CloudScope.Isolated,
        string cloud = "eu") =>
        new()
        {
            System = system,
            Service = service,
            ResourceType = type,
            Environment = env,
            Region = region,
            Stamp = stamp,
            Instance = instance,
            Scope = scope,
            Cloud = cloud,
        };

    private static string NameOf(
        ResourceTypeSpec type,
        string system,
        string? service = null,
        string env = "prod",
        string? region = null,
        string? stamp = null,
        string? instance = null,
        CloudScope scope = CloudScope.Isolated) =>
        Namer.Name(Request(type, system, service, env, region, stamp, instance, scope));

    // ---- Cloud-singleton (no region token) -------------------------------------------------------

    [Fact]
    public void ResourceGroup_system_slice() =>
        Assert.Equal("rg-ptk-prod", NameOf(AzureResourceTypes.ResourceGroup, "ptk"));

    [Fact]
    public void ServiceBus_namespace_singleton() =>
        Assert.Equal("ptk-sbns-prod", NameOf(AzureResourceTypes.ServiceBusNamespace, "ptk"));

    [Fact]
    public void Sql_logical_server() =>
        Assert.Equal("ptk-sql-prod", NameOf(AzureResourceTypes.SqlServer, "ptk"));

    [Fact]
    public void Sql_database() =>
        Assert.Equal("ptk-mbr-sqldb-prod", NameOf(AzureResourceTypes.SqlDatabase, "ptk", "mbr"));

    [Fact]
    public void Storage_shared_blob_is_condensed() =>
        Assert.Equal("ptkmbrstprod", NameOf(AzureResourceTypes.StorageAccount, "ptk", "mbr"));

    [Fact]
    public void Container_registry_is_condensed() =>
        Assert.Equal("ptkcrprod", NameOf(AzureResourceTypes.ContainerRegistry, "ptk"));

    [Fact]
    public void Log_analytics_singleton() =>
        Assert.Equal("ptk-log-prod", NameOf(AzureResourceTypes.LogAnalytics, "ptk"));

    // ---- Regional (region token included) --------------------------------------------------------

    [Fact]
    public void ResourceGroup_service_slice_with_stamp() =>
        Assert.Equal("rg-ptk-mbr-prod-weu-001",
            NameOf(AzureResourceTypes.ResourceGroup, "ptk", "mbr", region: "weu", stamp: "001"));

    [Fact]
    public void KeyVault_regional() =>
        Assert.Equal("ptk-mbr-kv-prod-weu", NameOf(AzureResourceTypes.KeyVault, "ptk", "mbr", region: "weu"));

    [Fact]
    public void AppService_regional() =>
        Assert.Equal("ptk-mbr-app-prod-weu", NameOf(AzureResourceTypes.AppService, "ptk", "mbr", region: "weu"));

    [Fact]
    public void ManagedIdentity_regional_with_instance() =>
        Assert.Equal("ptk-mbr-id-prod-weu-001",
            NameOf(AzureResourceTypes.ManagedIdentity, "ptk", "mbr", region: "weu", instance: "001"));

    // ---- Same type, different scope / instance ---------------------------------------------------

    [Fact]
    public void Storage_event_hub_checkpoint_is_regional() =>
        Assert.Equal("ptkmbrstprodweu", NameOf(AzureResourceTypes.StorageAccount, "ptk", "mbr", region: "weu"));

    [Fact]
    public void Storage_same_scope_duplicate_uses_instance() =>
        Assert.Equal("ptkmbrstprod01", NameOf(AzureResourceTypes.StorageAccount, "ptk", "mbr", instance: "01"));

    // ---- Shared scope adds a deterministic uniqueness suffix --------------------------------------

    [Fact]
    public void Shared_scope_appends_five_char_suffix_to_dns_global_type()
    {
        var name = NameOf(AzureResourceTypes.StorageAccount, "ptk", "mbr", scope: CloudScope.Shared);

        Assert.StartsWith("ptkmbrstprod", name);
        Assert.Equal("ptkmbrstprod".Length + 5, name.Length);
        Assert.Matches("^ptkmbrstprod[a-z0-9]{5}$", name);
    }

    [Fact]
    public void Shared_scope_suffix_is_deterministic()
    {
        var a = NameOf(AzureResourceTypes.StorageAccount, "ptk", "mbr", scope: CloudScope.Shared);
        var b = NameOf(AzureResourceTypes.StorageAccount, "ptk", "mbr", scope: CloudScope.Shared);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Non_dns_global_type_gets_no_suffix_in_shared_scope() =>
        Assert.Equal("ptk-mbr-id-prod-weu",
            NameOf(AzureResourceTypes.ManagedIdentity, "ptk", "mbr", region: "weu", scope: CloudScope.Shared));

    // ---- Length budget: fail, don't truncate -----------------------------------------------------

    [Fact]
    public void Env_falls_back_to_one_char_only_to_fit_the_budget()
    {
        // Full env "stage" overflows Storage's 24 chars (25); the 1-char fallback "s" fits (21).
        var name = NameOf(AzureResourceTypes.StorageAccount, "abcd", "mbrxy",
            env: "stage", region: "weu", stamp: "001", instance: "001");

        Assert.Equal("abcdmbrxystsweu001001", name);
        Assert.True(name.Length <= AzureResourceTypes.StorageAccount.MaxLength);
    }

    [Fact]
    public void Overflow_throws_rather_than_truncating()
    {
        var request = Request(AzureResourceTypes.StorageAccount, "toolong", "alsotoolong",
            region: "weu", stamp: "001", instance: "001");

        Assert.Throws<ResourceNameOverflowException>(() => Namer.Name(request));
    }
}
