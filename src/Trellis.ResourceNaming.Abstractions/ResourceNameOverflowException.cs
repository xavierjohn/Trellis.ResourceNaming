namespace Trellis.ResourceNaming;

/// <summary>
/// Thrown when a computed name would exceed its resource type's maximum length. The convention fails loudly
/// rather than silently truncating a disambiguating token into a collision.
/// </summary>
public sealed class ResourceNameOverflowException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the overflow.</param>
    public ResourceNameOverflowException(string message)
        : base(message)
    {
    }
}
