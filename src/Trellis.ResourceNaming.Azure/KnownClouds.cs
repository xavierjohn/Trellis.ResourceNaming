namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// String identifiers for the well-known Azure cloud environments, matching the conventional Azure ARM
/// environment names (e.g. <c>AzureCloud</c>, <c>AzureUSGovernment</c>).
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
