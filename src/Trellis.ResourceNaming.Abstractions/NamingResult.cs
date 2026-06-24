namespace Trellis.ResourceNaming;

/// <summary>A computed resource name and the governance tags that should accompany it.</summary>
/// <param name="Name">The resource name.</param>
/// <param name="Tags">The governance tags (system, service, env, region, role/purpose, cloud, …).</param>
public sealed record NamingResult(string Name, IReadOnlyDictionary<string, string> Tags);
