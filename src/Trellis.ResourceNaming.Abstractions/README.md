# Trellis.ResourceNaming.Abstractions

Store- and cloud-agnostic abstractions for convention-based resource naming.

This package holds the contract only. It has no cloud SDK dependency and no naming rules of its
own — it defines the seam that a convention implements, so application and infrastructure code can
depend on *"something names resources"* without depending on **how** they are named or on which
cloud they live in.

For the Azure CAF-aligned convention, install
[`Trellis.ResourceNaming.Azure`](https://www.nuget.org/packages/Trellis.ResourceNaming.Azure),
which references this package.

## Install

```shell
dotnet add package Trellis.ResourceNaming.Abstractions
```

Install this directly only when you are implementing your own convention, or when a library needs
to accept an `IResourceNamer` without binding to one. Applications normally install the
implementation package instead and get this transitively.

## What's in the box

| Type | Purpose |
| --- | --- |
| `IResourceNamer` | The seam. `string Name(NamingRequest request)`. |
| `NamingRequest` | The naming inputs: `System`, `Service`, `ResourceType`, `Environment`, `Region`, `Stamp`, `Instance`, `Cloud`, `Scope`. |
| `ResourceTypeSpec` | What a resource type permits: `Abbreviation`, `MinLength`, `MaxLength`, `Separator`, `IsDnsGlobal`. |
| `CloudScope` | Whether a name must be globally unique (`Shared`) or only locally (`Private`). |
| `NameSeparator` | The separator a resource type accepts, including *none*. |
| `ResourceNameOverflowException` | Thrown when a name cannot fit the type's length budget. |

## Why a separate spec type

`ResourceTypeSpec` is public and constructible, so a caller is never blocked by a gap in an
implementation's catalog. If a convention package does not yet know a resource type, you can supply
the spec at the call site and still get a name that follows the convention:

```csharp
var spec = new ResourceTypeSpec(
    Abbreviation: "cog",
    MinLength: 2,
    MaxLength: 64,
    Separator: NameSeparator.Dash,
    IsDnsGlobal: false);

var name = namer.Name(new NamingRequest
{
    System = "contoso",
    ResourceType = spec,
    Environment = "prod",
    Cloud = "az",
});
```

## Failure model

Names are validated, never silently repaired. When the inputs cannot produce a legal name,
`Name` throws `ResourceNameOverflowException` rather than truncating to something that
looks plausible and collides later. Naming is a deployment-time concern, so failing loudly at
startup is preferable to discovering a mangled name in production.

## AI-native

This package ships an API reference for coding agents at `trellis/trellis-api-resourcenaming.md`
and copies it into the consuming repository's `.github/` directory at build time, covering both
this package and `Trellis.ResourceNaming.Azure`.

## Links

- [Repository](https://github.com/xavierjohn/Trellis.ResourceNaming)
- [License: MIT](https://github.com/xavierjohn/Trellis.ResourceNaming/blob/main/LICENSE)
