namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// String identifiers for the well-known Azure cloud environments. The values match the ASP template's
/// <c>CloudType</c> constants so the two stay interchangeable.
/// </summary>
public static class KnownClouds
{
    /// <summary>Azure public (commercial) cloud.</summary>
    public const string AzureCloud = nameof(AzureCloud);

    /// <summary>Azure US Government cloud.</summary>
    public const string AzureUSGovernment = nameof(AzureUSGovernment);

    /// <summary>Azure China (21Vianet) cloud.</summary>
    public const string AzureChinaCloud = nameof(AzureChinaCloud);
}
