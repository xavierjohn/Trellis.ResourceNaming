using System.Reflection;
using Xunit;

namespace Trellis.ResourceNaming.Azure.Tests;

public class StrongNameTests
{
    private const string ExpectedPublicKeyToken = "30edd03a0eb2b9d7";

    [Fact]
    public void AbstractionsAssembly_when_built_has_expected_strong_name_identity() =>
        AssertPublicKeyToken(typeof(NamingPolicy).Assembly);

    [Fact]
    public void AzureAssembly_when_built_has_expected_strong_name_identity() =>
        AssertPublicKeyToken(typeof(AzureResourceNamer).Assembly);

    private static void AssertPublicKeyToken(Assembly assembly)
    {
        var publicKeyToken = assembly.GetName().GetPublicKeyToken();
        var actual = publicKeyToken is { Length: > 0 }
            ? Convert.ToHexString(publicKeyToken).ToLowerInvariant()
            : string.Empty;

        Assert.Equal(ExpectedPublicKeyToken, actual);
    }
}
