# Trellis.ResourceNaming

Deterministic, length-safe naming for cloud resources.

Given an environment, a region and a resource type, these libraries produce the same
resource name every time — trimmed to the platform's length limit, restricted to the
character set the resource type allows, and suffixed with a short hash so that globally
scoped names stay unique across deployments.

| Package | Purpose |
|---|---|
| `Trellis.ResourceNaming.Abstractions` | Vendor-neutral naming engine: `NamingPolicy`, `ResourceTypeSpec`, `NamingRequest`, `IResourceNamer` |
| `Trellis.ResourceNaming.Azure` | Azure resource-type catalog, cloud endpoint suffixes, and `DeployedEnvironmentOptions` |

```csharp
var context = new DeployedEnvironmentOptions
{
    Product = "ptk",
    Service = "mbr",
    Environment = "prod",
    RegionShortName = "weu",
};

context.StorageName();                                  // ptkmbrstproduuwsm
context.Name(AzureResourceTypes.SqlDatabase);           // named accessor
context.Name(new ResourceTypeSpec("plan", 1, 40, NameSeparator.Dash, IsDnsGlobal: false),
             region: "weu");                            // escape hatch for uncatalogued types
```

`Trellis.ResourceNaming.Azure` takes **no dependency on any Azure SDK**. It is a catalog of
naming rules and endpoint strings; "Azure" describes the vocabulary, not the coupling.

## Names cannot be reproduced in IaC

The uniqueness suffix is a SHA-256 digest rendered in base 36. Bicep, ARM and Terraform
cannot reproduce it. Names must be computed here and passed *into* the infrastructure
templates as parameters — never recomputed there, or the deployed name will drift from the
one the application resolves at runtime.

## Documentation

`docs/api_reference/trellis-api-resourcenaming.md` is written for LLM coding agents and
ships inside the `Trellis.ResourceNaming.Abstractions` package. On build it is copied into
the consuming repository's `.github/` folder, so an agent working in that repository finds
it without a separate fetch. It is organised as a decision table and a ranked list of traps,
because nearly every misuse of this library produces a wrong name at runtime rather than a
compile error.

## Layout

```
src/      the two packages and their tests
build/    API reference packaging: delivery targets and the CI gate that verifies it
docs/     the LLM API reference that ships in the package
```

`build/Trellis.ApiReference.targets` is a verbatim copy of the file in
[xavierjohn/Trellis](https://github.com/xavierjohn/Trellis). Neither package here has
`Trellis.Core` in its transitive closure, so this repository must ship the copy logic
itself. Fix bugs upstream and re-copy rather than editing it here.

## Building

```powershell
dotnet build Trellis.ResourceNaming.slnx -c Release
dotnet test  Trellis.ResourceNaming.slnx -c Release
./build/test-apireference-packaging.ps1
```

The third command is not optional before a release. `0.1.0-preview.1` shipped with a green
build and zero documentation inside the package, because nothing inspected the packed
output; that gate exists so it cannot happen again.

## Releasing

Two channels, both manually dispatched:

| Workflow | Target | Auth |
|---|---|---|
| `publish-nuget.yml` — *Publish to NuGet.org* | public nuget.org | Trusted Publishing (OIDC) |
| `publish-github-packages.yml` — *Publish to GitHub Packages* | internal alpha feed | built-in `GITHUB_TOKEN` |

The nuget.org workflow defaults to a dry run; set `dry_run = false` to publish. Both pack
once and run the API reference gate against those exact artifacts before pushing them.

The Trusted Publishing policy is bound to the **workflow file name**. Renaming
`publish-nuget.yml` invalidates the policy and publishing will fail to authenticate until
the policy is updated to match.

Version comes from [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning):
`version.json` at the repository root sets `0.1-preview.{height}`, so the patch/prerelease
number advances with git height rather than being edited by hand. Builds off `main` (or a
`release/vX.Y` branch) are public releases and produce a clean `0.1.0-preview.N`; every other
branch appends a `.g<commit>` suffix (for example `0.1.0-preview.10.g898ee61829`), which is
why a PR build's package is not publishable.
Cut a release branch with `nbgv prepare-release`.

## History

Extracted from [xavierjohn/Trellis.Templates](https://github.com/xavierjohn/Trellis.Templates)
with history preserved. The templates consumed these libraries as published NuGet packages,
so the repository depended on its own published output — every library change required a
publish and a re-pin in the same repository. The templates still consume the packages from
nuget.org; only the source moved.
