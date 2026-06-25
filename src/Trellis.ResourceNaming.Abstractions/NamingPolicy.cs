namespace Trellis.ResourceNaming;

/// <summary>
/// Identifies the naming-policy contract version. Names are persistent infrastructure identifiers, so a
/// change in how names are computed is a versioned, opt-in event — not an accident.
/// </summary>
public static class NamingPolicy
{
    /// <summary>The naming-policy contract version.</summary>
    public const string Version = "1.0";
}
