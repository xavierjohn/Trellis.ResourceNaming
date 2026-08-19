#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verifies that the ResourceNaming packages deliver the LLM API reference and carry correct
    listing metadata.

.DESCRIPTION
    The API reference reaches a consumer only if three independent things hold. Each can break
    silently - the build stays green, the tests stay green, and the docs simply never appear:

      1. Trellis.ResourceNaming.Abstractions packs the doc under trellis/.
      2. It packs the copy logic at BOTH build/<id>.targets and buildTransitive/<id>.targets.
         Neither package has Trellis.Core in its transitive closure, so nothing else will
         supply the copy logic.
      3. Trellis.ResourceNaming.Azure's dependency on Abstractions does NOT exclude Build
         assets. This is the dangerous one: the SDK's DEFAULT for a ProjectReference emits
         exclude="Build,Analyzers", which suppresses buildTransitive and delivers nothing to
         anyone referencing only .Azure - the way almost every consumer references this family.
         It is corrected by PrivateAssets="none" on the ProjectReference, and removing that
         attribute reintroduces the bug with no other visible symptom.

    It then checks the nuspec listing metadata on both packages: icon, README, and the
    projectUrl/repository URLs. See the comment on that block for why.

.NOTES
    Exit code 0 = all checks passed. Non-zero = at least one check failed.

    By default the script packs into a temporary directory and cleans up after itself.
    Pass -PackageDirectory to verify packages that have ALREADY been packed. The publish
    workflow uses that mode so the artifacts it inspects are byte-for-byte the artifacts
    it pushes, rather than a second pack that merely ought to be identical.
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',

    # Verify pre-packed .nupkg files in this directory instead of packing. The directory is
    # left alone on exit; only a directory this script created is cleaned up.
    [string] $PackageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'Trellis.ResourceNaming.slnx'

$packedHere = [string]::IsNullOrWhiteSpace($PackageDirectory)
if ($packedHere) {
    $outDir = Join-Path ([System.IO.Path]::GetTempPath()) "rn-pack-gate-$([System.Guid]::NewGuid().ToString('N'))"
}
else {
    $outDir = (Resolve-Path -Path $PackageDirectory).Path
}

$failures = [System.Collections.Generic.List[string]]::new()

function Assert-True {
    param(
        [bool] $Condition,
        [string] $Message,
        [string] $Detail
    )
    if ($Condition) {
        Write-Host "  PASS  $Message"
    }
    else {
        Write-Host "  FAIL  $Message"
        if ($Detail) { Write-Host "        $Detail" }
        $script:failures.Add($Message)
    }
}

try {
    if ($packedHere) {
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null

        Write-Host "Packing $solution -> $outDir"
        $packLog = & dotnet pack $solution -c $Configuration -o $outDir 2>&1
        if ($LASTEXITCODE -ne 0) {
            $packLog | Write-Host
            throw "dotnet pack failed with exit code $LASTEXITCODE."
        }
    }
    else {
        Write-Host "Verifying pre-packed output in $outDir"
        if (-not (Get-ChildItem -Path $outDir -Filter '*.nupkg' -File)) {
            throw "No .nupkg files found in '$outDir'. Run dotnet pack before invoking with -PackageDirectory."
        }
    }

    # PowerShell 7 already exposes System.IO.Compression.ZipFile; Add-Type is a no-op there and a
    # necessary load on Windows PowerShell. Failure to load is not fatal if the type is present.
    try { Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop } catch { }
    if (-not ('System.IO.Compression.ZipFile' -as [type])) {
        throw 'System.IO.Compression.ZipFile is unavailable; cannot inspect packages.'
    }

    function Get-Nupkg {
        param([string] $Id)
        $match = Get-ChildItem -Path $outDir -Filter "$Id.*.nupkg" -File |
            Where-Object { $_.Name -notlike '*.symbols.nupkg' } |
            Select-Object -First 1
        if (-not $match) { throw "No package produced for '$Id'. Is it still packable?" }
        return $match.FullName
    }

    function Get-Entries {
        param([string] $Path)
        $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
        try { return @($zip.Entries | ForEach-Object { $_.FullName }) }
        finally { $zip.Dispose() }
    }

    function Get-Nuspec {
        param([string] $Path)
        $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
        try {
            $entry = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
            if (-not $entry) { throw "No .nuspec inside '$Path'." }
            $reader = [System.IO.StreamReader]::new($entry.Open())
            try { return [xml]$reader.ReadToEnd() }
            finally { $reader.Dispose() }
        }
        finally { $zip.Dispose() }
    }

    # --- Abstractions: payload + copy logic -------------------------------------------------
    $abstractionsId = 'Trellis.ResourceNaming.Abstractions'
    $abstractionsPkg = Get-Nupkg $abstractionsId
    $entries = Get-Entries $abstractionsPkg

    Write-Host ''
    Write-Host "$abstractionsId ($(Split-Path -Leaf $abstractionsPkg))"

    $docs = @($entries | Where-Object { $_ -like 'trellis/*.md' })
    Assert-True ($docs.Count -gt 0) "packs at least one API reference under trellis/"
    Assert-True ($docs -contains 'trellis/trellis-api-resourcenaming.md') `
        "packs trellis/trellis-api-resourcenaming.md" `
        "actual trellis/ entries: $($docs -join ', ')"

    Assert-True ($entries -contains "build/$abstractionsId.targets") `
        "packs build/$abstractionsId.targets (direct reference)"
    Assert-True ($entries -contains "buildTransitive/$abstractionsId.targets") `
        "packs buildTransitive/$abstractionsId.targets (transitive reference)"

    # --- Azure: must not suppress the build assets that carry the copy logic -----------------
    $azureId = 'Trellis.ResourceNaming.Azure'
    $azurePkg = Get-Nupkg $azureId
    $nuspec = Get-Nuspec $azurePkg

    Write-Host ''
    Write-Host "$azureId ($(Split-Path -Leaf $azurePkg))"

    $dependency = $nuspec.package.metadata.dependencies.group.dependency |
        Where-Object { $_.id -eq $abstractionsId } |
        Select-Object -First 1

    Assert-True ($null -ne $dependency) "declares a dependency on $abstractionsId"

    if ($dependency) {
        # 'exclude' is absent when PrivateAssets="none"; the SDK default would emit "Build,Analyzers".
        $exclude = if ($dependency.HasAttribute('exclude')) { $dependency.GetAttribute('exclude') } else { '' }
        Assert-True ($exclude -notmatch '(?i)\bbuild\b') `
            "does not exclude Build assets from the $abstractionsId dependency" `
            ("exclude='$exclude'. Excluding Build suppresses buildTransitive, so a consumer referencing " +
             "only $azureId would receive no API reference at all. Restore PrivateAssets=`"none`" on the " +
             "ProjectReference in $azureId.csproj.")
    }

    # --- Both packages: listing metadata ------------------------------------------------------
    # 0.1.0-preview.2 shipped with no icon on either package, no README on Abstractions, and
    # projectUrl/repository still pointing at xavierjohn/Trellis.Templates - the repository this
    # code was extracted from. The stale repository URL is the worst of the three: SourceLink
    # stamps a real commit SHA next to it, so a consumer stepping into the library is sent to a
    # commit that does not exist in the repository named. All three passed every check that
    # existed, because nothing inspected the nuspec.
    $expectedRepo = 'https://github.com/xavierjohn/Trellis.ResourceNaming'

    # Set-StrictMode turns a missing nuspec element into a terminating "property cannot be found"
    # error, so $meta.icon would crash the gate on exactly the package it is meant to report on.
    # Absence is the condition under test, not an error.
    function Get-MetaValue {
        param($Metadata, [string] $Name)
        $property = $Metadata.PSObject.Properties[$Name]
        if ($property) { return $property.Value }
        return $null
    }

    foreach ($id in @($abstractionsId, $azureId)) {
        $pkg = Get-Nupkg $id
        $pkgEntries = Get-Entries $pkg
        $meta = (Get-Nuspec $pkg).package.metadata

        Write-Host ''
        Write-Host "$id (listing metadata)"

        $icon = Get-MetaValue $meta 'icon'
        Assert-True ([bool]$icon -and ($pkgEntries -contains $icon)) `
            "packs the Trellis icon and declares it" `
            "nuspec <icon>='$icon'; matching entry present: $($pkgEntries -contains $icon)"

        $readme = Get-MetaValue $meta 'readme'
        Assert-True ([bool]$readme -and ($pkgEntries -contains $readme)) `
            "packs a listing README and declares it" `
            "nuspec <readme>='$readme'; matching entry present: $($pkgEntries -contains $readme)"

        $projectUrl = Get-MetaValue $meta 'projectUrl'
        Assert-True ($projectUrl -eq $expectedRepo) `
            "points projectUrl at this repository" `
            "projectUrl='$projectUrl', expected '$expectedRepo'"

        $repository = Get-MetaValue $meta 'repository'
        $repoUrl = if ($repository) { $repository.url } else { $null }
        Assert-True ($repoUrl -eq "$expectedRepo.git") `
            "points repository url at this repository" `
            ("repository url='$repoUrl', expected '$expectedRepo.git'. A stale URL combined with the " +
             "SourceLink commit SHA sends debuggers to a commit that does not exist there.")
    }

    # Symbol packages are deliberately not shipped for this family; DotNet.ReproducibleBuilds is
    # capable of turning them on, so assert rather than assume. Both formats matter: the modern
    # .snupkg and the legacy .symbols.nupkg, which is the dangerous one because it ends in .nupkg
    # and is therefore swept up by the publish workflows' "nupkg/*.nupkg" push glob.
    $symbolPackages = @(
        Get-ChildItem -Path $outDir -File |
            Where-Object { $_.Name -like '*.snupkg' -or $_.Name -like '*.symbols.nupkg' } |
            ForEach-Object { $_.Name }
    )
    Assert-True ($symbolPackages.Count -eq 0) `
        "produces no symbol packages" `
        "found: $($symbolPackages -join ', ')"

    Write-Host ''
    if ($failures.Count -gt 0) {
        Write-Host "FAILED - $($failures.Count) check(s) did not pass." -ForegroundColor Red
        exit 1
    }

    Write-Host "All API reference packaging checks passed." -ForegroundColor Green
    exit 0
}
finally {
    if ($packedHere -and (Test-Path $outDir)) { Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue }
}
