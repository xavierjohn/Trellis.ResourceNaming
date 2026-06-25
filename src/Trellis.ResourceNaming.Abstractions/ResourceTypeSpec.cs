namespace Trellis.ResourceNaming;

/// <summary>
/// The naming rules for a single resource type: its abbreviation, length budget, token separator, and
/// whether its name is globally DNS-scoped (and therefore needs a uniqueness suffix in
/// <see cref="CloudScope.Shared"/>).
/// </summary>
/// <param name="Abbreviation">The CAF-aligned type abbreviation (e.g. <c>st</c>, <c>kv</c>).</param>
/// <param name="MinLength">Minimum allowed name length.</param>
/// <param name="MaxLength">Maximum allowed name length.</param>
/// <param name="Separator">How tokens are joined.</param>
/// <param name="IsDnsGlobal">Whether the name is globally unique / DNS-scoped.</param>
public sealed record ResourceTypeSpec(
    string Abbreviation,
    int MinLength,
    int MaxLength,
    NameSeparator Separator,
    bool IsDnsGlobal);
