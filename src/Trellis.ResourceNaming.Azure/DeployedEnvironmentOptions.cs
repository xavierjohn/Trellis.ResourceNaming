namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// The deployed-environment context for a service — bound once from configuration — that every consumer reads.
/// The resource-naming accessors infer resource names and endpoint URLs from it (the analog of the ASP
/// template's <c>EnvironmentOptions</c>), and an SLI caller builds its location id from the cloud moniker and
/// <see cref="Region"/>. Set these few values once; ask for names, URLs, or the location parts by accessor.
/// </summary>
public sealed class DeployedEnvironmentOptions
{
    /// <summary>Product / platform short code (e.g. <c>ptk</c>).</summary>
    public string System { get; set; } = string.Empty;

    /// <summary>Bounded-context / service short code (e.g. <c>mbr</c>). Omit for system-shared resources.</summary>
    public string? Service { get; set; }

    /// <summary>Environment / lifecycle as a CAF word (e.g. <c>prod</c>).</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Full Azure region name (e.g. <c>westus3</c>), used for the SLI location id and display. It is never a
    /// name token — resource names use <see cref="RegionShortName"/>.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>Short region code (e.g. <c>weu</c>) used as the region token in regional resource names.</summary>
    public string? RegionShortName { get; set; }

    /// <summary>Azure cloud environment — a <see cref="KnownClouds"/> value that selects the endpoint host suffix.</summary>
    public string Cloud { get; set; } = KnownClouds.AzureCloud;

    /// <summary>Isolation scope. Defaults to <see cref="CloudScope.Isolated"/>.</summary>
    public CloudScope Scope { get; set; } = CloudScope.Isolated;

    /// <summary>Immutable scale-unit / cell ordinal, if the workload is stamped.</summary>
    public string? Stamp { get; set; }
}
