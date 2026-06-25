namespace Trellis.ResourceNaming;

/// <summary>
/// Whether a resource lives in a fully isolated cloud (its own DNS namespaces) or in a shared, multi-tenant
/// DNS namespace where globally-scoped names must be disambiguated.
/// </summary>
public enum CloudScope
{
    /// <summary>
    /// Air-gapped / sovereign / single-tenant cloud. DNS namespaces are private, so no global-uniqueness
    /// suffix is added to names.
    /// </summary>
    Isolated,

    /// <summary>A shared, multi-tenant DNS namespace. Globally DNS-scoped names receive a short uniqueness suffix.</summary>
    Shared,
}
