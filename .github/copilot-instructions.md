# GitHub Copilot Instructions for Trellis.ResourceNaming

## Project overview

Two packages that turn a deployment context into deterministic, length-safe cloud resource
names:

| Package | Contents |
|---|---|
| `Trellis.ResourceNaming.Abstractions` | vendor-neutral engine — `NamingPolicy`, `ResourceTypeSpec`, `NamingRequest`, `IResourceNamer`, `CloudScope` |
| `Trellis.ResourceNaming.Azure` | Azure resource-type catalog, cloud endpoint suffixes, `DeployedEnvironmentOptions` |

`Trellis.ResourceNaming.Azure` takes **no dependency on any Azure SDK**. "Azure" describes
the naming vocabulary, not the coupling. Do not add one; if a change appears to need an
Azure SDK type in the shipped surface, that is a design signal, not a dependency to add.

## API usage source of truth

Read `docs/api_reference/trellis-api-resourcenaming.md` before writing or changing code that
uses these APIs. It is the same file that ships inside the package, and it is organised as a
decision table and a ranked list of traps rather than a member dump — because nearly every
misuse here produces a **wrong name at runtime**, not a compile error.

Do not infer behaviour from these instructions or from prior knowledge of similar libraries.

The traps that actually bite, in order:

1. **Named accessor vs. generic escape hatch.** `ResourceGroupName()` requires a region.
   Reaching for it on a global (region-less) stack silently produces the wrong name.
2. **The uniqueness suffix cannot be reproduced in IaC.** It is a SHA-256 digest rendered in
   base 36. Bicep, ARM and Terraform cannot recompute it. Names are computed here and passed
   *into* templates as parameters. Never recompute a name in the IaC, or the deployed name
   drifts from the one the application resolves at runtime.
3. **Cloud-dependent endpoint suffixes** — `AzureUSGovernment` yields `usgovcloudapi.net`.
4. **Regional vs. global pairing** — region and region-short are all-or-nothing; supplying
   one without the other silently falls back.
5. **Length caps** — several types cap below the platform limit, and the hash suffix can push
   a name past the cap, which truncates the environment segment rather than failing.

Note that `NamingRequest.Scope` and `DeployedEnvironmentOptions.Scope` both default to
`Shared`, while most existing tests use `CloudScope.Isolated`. Behaviour on the default path
is therefore less covered than test count suggests — verify against a real call, not against
a nearby test.

## Test-driven development

1. Add or update a failing test that proves the bug or specifies the new behaviour.
2. Implement the smallest correct change.
3. Refactor while keeping tests green.

Do not skip the red step. Tests live in `src/Trellis.ResourceNaming.Azure.Tests/` and run on
**xunit v3 over Microsoft.Testing.Platform**, matching `xavierjohn/Trellis`.

MTP is not VSTest. `--nologo` and `--filter` are **not** valid arguments: passing either makes
the run exit with code 5 and "Zero tests ran", which looks like a pass in a CI log. Invoke as
`dotnet test Trellis.ResourceNaming.slnx -c Release` and narrow output with `Select-String`
instead. Always confirm the summary reports the expected test total, not zero.

Assert on exact expected name strings. A test that asserts only a prefix or a length will
pass through the exact defects this library exists to prevent.

## Documentation

Any change to public API surface requires a matching update to
`docs/api_reference/trellis-api-resourcenaming.md`. That file ships in the package, so a
stale doc is a shipped defect, not an internal inconsistency.

Adding a resource type to the Azure catalog means updating the catalog table in that doc.

## Packaging

The API reference reaches consumers through MSBuild targets packed into the `.nupkg`. This is
silent when broken: the build stays green, the tests stay green, and no documentation is
delivered. `0.1.0-preview.1` shipped exactly that way.

- `build/Trellis.ApiReference.targets` is a **verbatim copy** from `xavierjohn/Trellis`.
  Fix bugs upstream and re-copy. Editing it here creates federation drift, and which copy
  wins then depends on NuGet import order.
- Neither package has `Trellis.Core` in its transitive closure, so this repository must ship
  the copy logic, not just the doc payload.
- `Trellis.ResourceNaming.Azure` needs `PrivateAssets="none"` on its `ProjectReference`. The
  SDK default packs the dependency as `exclude="Build,Analyzers"`, which suppresses
  `buildTransitive` and delivers nothing to anyone referencing only `.Azure`.
- Use **forward slashes** in `PackagePath`. A trailing backslash yields `trellis//<name>.md`
  on Linux, which still satisfies a glob check and so passes casual inspection.

Run `./build/test-apireference-packaging.ps1` after touching any of the above.

## Validation before handoff

```powershell
dotnet build Trellis.ResourceNaming.slnx -c Release
dotnet test  Trellis.ResourceNaming.slnx -c Release
./build/test-apireference-packaging.ps1
```

The third is not optional when packaging, project references, or the doc are touched.

## Releasing

`publish-nuget.yml` (public, Trusted Publishing) and `publish-github-packages.yml` (internal
alpha). Trusted Publishing binds the policy to the **workflow file name** — renaming
`publish-nuget.yml` breaks authentication until the nuget.org policy is updated to match.

Version comes from Nerdbank.GitVersioning (`version.json`, `0.1-preview.{height}`), not from
`VersionPrefix`/`VersionSuffix`. Do not add either property to `src/Directory.Build.props` —
setting one overrides the computed version and silently restores hand-maintained numbers.
Only `main` and `release/v*` are public-release refs; other branches get a `.g<commit>`
suffix (for example `0.1.0-preview.10.g898ee61829`) and must not be published. The publish
guard matches that exact shape, so keep the two in step. Any workflow that builds must check
out with `fetch-depth: 0` — Nerdbank.GitVersioning fails the build outright on a shallow
clone with "Shallow clone lacks the objects required to calculate version height".

## Git and PR rules

- Do not commit without explicit user approval.
- Do not push branches, create pull requests, or merge them unless explicitly asked.
- Do not amend, rebase pushed history, or force-push unless explicitly asked.
