namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// The CAF-aligned naming rules for the Azure resource types covered by the convention. Each entry pins the
/// type abbreviation, length budget, token separator, and whether the name is globally DNS-scoped.
/// </summary>
public static class AzureResourceTypes
{
    /// <summary>Storage account — condensed, 3–24 chars, globally unique.</summary>
    public static readonly ResourceTypeSpec StorageAccount = new("st", 3, 24, NameSeparator.None, IsDnsGlobal: true);

    /// <summary>Key Vault — dashed, 3–24 chars, globally unique.</summary>
    public static readonly ResourceTypeSpec KeyVault = new("kv", 3, 24, NameSeparator.Dash, IsDnsGlobal: true);

    /// <summary>Service Bus namespace — dashed, 6–50 chars, globally unique.</summary>
    public static readonly ResourceTypeSpec ServiceBusNamespace = new("sbns", 6, 50, NameSeparator.Dash, IsDnsGlobal: true);

    /// <summary>Event Hubs namespace — dashed, 6–50 chars, globally unique.</summary>
    public static readonly ResourceTypeSpec EventHubsNamespace = new("evhns", 6, 50, NameSeparator.Dash, IsDnsGlobal: true);

    /// <summary>Cosmos DB account — dashed, 3–44 chars, globally unique.</summary>
    public static readonly ResourceTypeSpec CosmosAccount = new("cosmos", 3, 44, NameSeparator.Dash, IsDnsGlobal: true);

    /// <summary>SQL logical server — dashed, 1–63 chars, globally unique.</summary>
    public static readonly ResourceTypeSpec SqlServer = new("sql", 1, 63, NameSeparator.Dash, IsDnsGlobal: true);

    /// <summary>SQL database — dashed, 1–128 chars, scoped to its server.</summary>
    public static readonly ResourceTypeSpec SqlDatabase = new("sqldb", 1, 128, NameSeparator.Dash, IsDnsGlobal: false);

    /// <summary>App Service — dashed, 2–60 chars, globally unique (hostname).</summary>
    public static readonly ResourceTypeSpec AppService = new("app", 2, 60, NameSeparator.Dash, IsDnsGlobal: true);

    /// <summary>Container Registry — condensed, 5–50 chars, globally unique.</summary>
    public static readonly ResourceTypeSpec ContainerRegistry = new("cr", 5, 50, NameSeparator.None, IsDnsGlobal: true);

    /// <summary>User-assigned managed identity — dashed, 3–128 chars, scoped to its resource group.</summary>
    public static readonly ResourceTypeSpec ManagedIdentity = new("id", 3, 128, NameSeparator.Dash, IsDnsGlobal: false);

    /// <summary>Log Analytics workspace — dashed, 4–63 chars, scoped to its resource group.</summary>
    public static readonly ResourceTypeSpec LogAnalytics = new("log", 4, 63, NameSeparator.Dash, IsDnsGlobal: false);

    /// <summary>Application Insights — dashed, 1–260 chars, scoped to its resource group.</summary>
    public static readonly ResourceTypeSpec ApplicationInsights = new("appi", 1, 260, NameSeparator.Dash, IsDnsGlobal: false);

    /// <summary>Resource group — dashed, 1–90 chars, with the universal <c>rg-</c> prefix.</summary>
    public static readonly ResourceTypeSpec ResourceGroup = new("rg", 1, 90, NameSeparator.Dash, IsDnsGlobal: false);
}
