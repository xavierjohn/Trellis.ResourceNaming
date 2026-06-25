namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// The deployment context for a service — bound once from configuration — from which every resource name and
/// endpoint is derived. The analog of the ASP template's <c>EnvironmentOptions</c>: set these few values and
/// ask for names/URLs by resource, without repeating the context or naming the resource type at each call.
/// </summary>
public sealed class AzureResourceNamingOptions
{
    /// <summary>Product / platform short code (e.g. <c>ptk</c>).</summary>
    public string System { get; set; } = string.Empty;

    /// <summary>Bounded-context / service short code (e.g. <c>mbr</c>). Omit for system-shared resources.</summary>
    public string? Service { get; set; }

    /// <summary>Environment / lifecycle as a CAF word (e.g. <c>prod</c>).</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>Physical region code for regional resources (e.g. <c>weu</c>).</summary>
    public string? Region { get; set; }

    /// <summary>Azure cloud environment — a <see cref="KnownClouds"/> value that selects the endpoint host suffix.</summary>
    public string Cloud { get; set; } = KnownClouds.AzureCloud;

    /// <summary>Isolation scope. Defaults to <see cref="CloudScope.Isolated"/>.</summary>
    public CloudScope Scope { get; set; } = CloudScope.Isolated;

    /// <summary>Immutable scale-unit / cell ordinal, if the workload is stamped.</summary>
    public string? Stamp { get; set; }
}
