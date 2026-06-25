using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Trellis.ResourceNaming.Azure;

/// <summary>
/// An opinionated, CAF-aligned implementation of the Trellis Azure resource-naming convention.
/// </summary>
/// <remarks>
/// Names are workload-first — <c>{system}-{service}-{type}-{env}-{region}-{stamp}-{instance}</c> — with the
/// universal <c>rg-</c> prefix for resource groups and condensed (separator-free) names for dashless types.
/// The environment is the full CAF word and falls back to a single character only when a name would exceed
/// its type's length budget. Tokens must be lowercase alphanumeric and the environment one of the CAF words
/// <c>local</c>/<c>test</c>/<c>stage</c>/<c>prod</c>; other inputs throw. Globally DNS-scoped types receive a
/// deterministic five-character uniqueness suffix in <see cref="CloudScope.Shared"/>. A name that still cannot
/// fit throws <see cref="ResourceNameOverflowException"/> rather than being truncated. The cloud is never a
/// name token; it selects the endpoint host suffix instead.
/// </remarks>
public sealed class AzureResourceNamer : IResourceNamer
{
    private const string HashAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    private static readonly string[] CafEnvironments = ["local", "test", "stage", "prod"];

    /// <inheritdoc />
    public string Name(NamingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ResourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.System);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Cloud);

        var spec = request.ResourceType;
        var system = ValidateToken(Normalize(request.System), nameof(NamingRequest.System))!;
        var service = ValidateToken(Normalize(request.Service), nameof(NamingRequest.Service));
        var region = ValidateToken(Normalize(request.Region), nameof(NamingRequest.Region));
        var stamp = ValidateToken(Normalize(request.Stamp), nameof(NamingRequest.Stamp));
        var instance = ValidateToken(Normalize(request.Instance), nameof(NamingRequest.Instance));
        var abbr = spec.Abbreviation.ToLowerInvariant();
        var envFull = ValidateEnvironment(Normalize(request.Environment)!);

        // Uniqueness suffix for globally DNS-scoped names in commercial Azure. Seeded on the canonical
        // identity (always the full env) so it is stable even if the env token later falls back.
        string? hash = spec.IsDnsGlobal && request.Scope == CloudScope.Shared
            ? Hash5(Seed(system, service, abbr, envFull, region, stamp, instance, Normalize(request.Cloud)!))
            : null;

        // Prefer the full CAF env word; fall back to one character only to fit the length budget.
        var name = Assemble(spec, system, service, abbr, envFull, region, stamp, instance, hash);
        if (name.Length > spec.MaxLength)
        {
            name = Assemble(spec, system, service, abbr, EnvShort(envFull), region, stamp, instance, hash);
        }

        if (name.Length > spec.MaxLength)
        {
            throw new ResourceNameOverflowException(
                $"Resource name '{name}' ({name.Length} chars) exceeds the {spec.MaxLength}-char limit for " +
                $"type '{abbr}'. Shorten the system/service codes — the convention fails rather than " +
                "truncating a disambiguating token into a collision.");
        }

        if (name.Length < spec.MinLength)
        {
            throw new ArgumentException(
                $"Resource name '{name}' ({name.Length} chars) is below the {spec.MinLength}-char minimum for " +
                $"type '{abbr}'. Use longer system/service codes.",
                nameof(request));
        }

        return name;
    }

    private static string Assemble(
        ResourceTypeSpec spec,
        string system,
        string? service,
        string abbr,
        string env,
        string? region,
        string? stamp,
        string? instance,
        string? hash)
    {
        var tokens = new List<string>(8);

        if (abbr == "rg")
        {
            // Resource group: universal rg- prefix, then workload-ordered. No separate type token; the
            // resource group is itself the slice, so it carries no instance token.
            tokens.Add("rg");
            tokens.Add(system);
            if (service is not null) tokens.Add(service);
            tokens.Add(env);
            if (region is not null) tokens.Add(region);
            if (stamp is not null) tokens.Add(stamp);
        }
        else
        {
            tokens.Add(system);
            if (service is not null) tokens.Add(service);
            tokens.Add(abbr);
            tokens.Add(env);
            if (region is not null) tokens.Add(region);
            if (stamp is not null) tokens.Add(stamp);
            if (instance is not null) tokens.Add(instance);
        }

        if (hash is not null) tokens.Add(hash);

        return spec.Separator == NameSeparator.Dash ? string.Join('-', tokens) : string.Concat(tokens);
    }

    private static string? Normalize(string? token) =>
        string.IsNullOrWhiteSpace(token) ? null : token.Trim().ToLowerInvariant();

    private static string EnvShort(string env) => env switch
    {
        "local" => "l",
        "test" => "t",
        "stage" => "s",
        "prod" => "p",
        _ => throw new InvalidOperationException($"Unsupported environment '{env}'."),
    };

    private static string? ValidateToken(string? token, string field)
    {
        if (token is null) return null;

        foreach (var c in token)
        {
            if (c is (< 'a' or > 'z') and (< '0' or > '9'))
            {
                throw new ArgumentException(
                    $"The {field} token '{token}' must be lowercase alphanumeric ([a-z0-9]); the convention " +
                    "inserts separators as needed.",
                    field);
            }
        }

        return token;
    }

    private static string ValidateEnvironment(string env)
    {
        if (Array.IndexOf(CafEnvironments, env) < 0)
        {
            throw new ArgumentException(
                $"Environment '{env}' is not a CAF environment word; use one of: " +
                $"{string.Join(", ", CafEnvironments)}.",
                nameof(NamingRequest.Environment));
        }

        return env;
    }

    private static string Seed(
        string system, string? service, string abbr, string env, string? region, string? stamp,
        string? instance, string cloud) =>
        string.Join('|', system, service ?? string.Empty, abbr, env, region ?? string.Empty,
            stamp ?? string.Empty, instance ?? string.Empty, cloud);

    private static string Hash5(string seed)
    {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(seed), digest);

        // Read with a fixed byte order so the suffix is identical on every platform, not host-endian.
        var value = BinaryPrimitives.ReadUInt64LittleEndian(digest);

        Span<char> chars = stackalloc char[5];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = HashAlphabet[(int)(value % 36)];
            value /= 36;
        }

        return new string(chars);
    }
}
