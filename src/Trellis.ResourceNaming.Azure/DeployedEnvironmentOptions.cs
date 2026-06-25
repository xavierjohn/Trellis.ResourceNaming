namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// The deployed-environment context for a service — bound once from configuration — that consumers read to
/// derive resource names and endpoint URLs (the analog of the ASP template's <c>EnvironmentOptions</c>). Set
/// these few values once; ask for names or URLs by accessor.
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
    /// Full Azure region name (e.g. <c>westus3</c>), for display and telemetry. It is never a name token —
    /// resource names use <see cref="RegionShortName"/>.
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
