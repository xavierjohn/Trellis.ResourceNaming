namespace Trellis.ResourceNaming;

/// <summary>The inputs used to compute a resource name.</summary>
public sealed record NamingRequest
{
    /// <summary>Product / platform short code (e.g. <c>ptk</c>).</summary>
    public required string System { get; init; }

    /// <summary>Bounded-context / service short code (e.g. <c>mbr</c>). Omit for system-shared resources.</summary>
    public string? Service { get; init; }

    /// <summary>The target resource type's naming rules.</summary>
    public required ResourceTypeSpec ResourceType { get; init; }

    /// <summary>Environment / lifecycle as a CAF word (e.g. <c>prod</c>).</summary>
    public required string Environment { get; init; }

    /// <summary>Physical region code for regional resources (e.g. <c>weu</c>). Omit for cloud-singletons.</summary>
    public string? Region { get; init; }

    /// <summary>Immutable scale-unit / cell ordinal (e.g. <c>001</c>). Omit when the workload is not stamped.</summary>
    public string? Stamp { get; init; }

    /// <summary>Disambiguator for multiple same-type resources sharing a scope in one slice (e.g. <c>001</c>).</summary>
    public string? Instance { get; init; }

    /// <summary>
    /// Cloud identifier. The namer treats it as an opaque token that only seeds the Shared-scope uniqueness
    /// suffix, so any non-empty value works; endpoint resolution (<c>AzureClouds.ByName</c>) additionally
    /// requires a <c>KnownClouds</c> value (e.g. <c>AzureCloud</c>). Never part of the name.
    /// </summary>
    public required string Cloud { get; init; }

    /// <summary>Isolation scope. Defaults to <see cref="CloudScope.Isolated"/>.</summary>
    public CloudScope Scope { get; init; } = CloudScope.Isolated;
}
