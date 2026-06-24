namespace Trellis.ResourceNaming;

/// <summary>How a resource type's name tokens are joined.</summary>
public enum NameSeparator
{
    /// <summary>Tokens are concatenated with no separator (e.g. Storage, Container Registry).</summary>
    None,

    /// <summary>Tokens are joined with hyphens.</summary>
    Dash,
}
