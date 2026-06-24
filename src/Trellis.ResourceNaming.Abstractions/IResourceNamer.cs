namespace Trellis.ResourceNaming;

/// <summary>Computes convention-based resource names and their governance tags.</summary>
public interface IResourceNamer
{
    /// <summary>Computes the name and tags for the given request.</summary>
    /// <param name="request">The naming inputs.</param>
    /// <returns>The computed name and tags.</returns>
    /// <exception cref="ResourceNameOverflowException">
    /// The name cannot fit the resource type's length budget.
    /// </exception>
    NamingResult Name(NamingRequest request);
}
