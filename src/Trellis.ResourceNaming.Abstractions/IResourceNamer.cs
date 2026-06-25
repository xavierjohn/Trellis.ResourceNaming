namespace Trellis.ResourceNaming;

/// <summary>Computes convention-based resource names.</summary>
public interface IResourceNamer
{
    /// <summary>Computes the name for the given request.</summary>
    /// <param name="request">The naming inputs.</param>
    /// <returns>The computed resource name.</returns>
    /// <exception cref="ResourceNameOverflowException">
    /// The name cannot fit the resource type's length budget.
    /// </exception>
    string Name(NamingRequest request);
}
