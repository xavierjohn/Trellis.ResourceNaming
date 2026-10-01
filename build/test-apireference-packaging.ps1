#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verifies that the ResourceNaming packages publish the LLM API reference as AgentDocs guidance and
    carry correct listing metadata.

.DESCRIPTION
    The API reference reaches a consumer only through the opt-in Trellis.AgentDocs local tool, and
    only if the package carries a correct guidance manifest. Each part can break silently - the build
    stays green, the tests stay green, and the guidance simply never installs:

      1. Trellis.ResourceNaming.Abstractions packs the doc at its root path, and a
         guidance/reference-manifest.json whose SHA-256, onDemand usage and "Open when ..."
         description match the packed bytes. It must be the only package that ships guidance, so a
         consumer approves exactly one package for both.
      2. Nothing in either package runs in a consumer's build: no build/ or buildTransitive/ assets, no
         trellis/ directory, and no dependency leaking the build-only Trellis.AgentDocs.Packaging
         helper. Restoring a package must never write to a consumer's repository.
      3. The published Trellis.AgentDocs tool accepts the packed Abstractions package under
         `validate --strict`: the manifest contract plus discoverability (links that leave the
         package, front matter, size budgets). A warning fails this gate like an error.
      4. Both READMEs tell a consumer how to opt in: install the tool, approve the package, sync.
      5. No PackagePath declares a backslash. A trailing backslash is a directory marker on Windows
         but not on Linux, where it normalizes and NuGet appends its own separator, producing
         malformed entries such as "dir//name". This declaration check is platform-independent and
         is what holds the line on a developer's Windows machine.

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

    # --- Declarations: PackagePath must not contain a backslash ------------------------------
    # Parses the XML rather than scanning text. A line-based regex misses the single-quoted
    # attribute form (PackagePath='trellis\') and the <PackagePath>trellis\</PackagePath>
    # metadata element, and would flag the comments that quote the malformed value on purpose.
    # Property indirection - PackagePath="$(SomeVar)" - is not statically visible; the packed
    # path assertions below cover that case on Linux.
    $backslashPaths = @(
        Get-ChildItem -Path $repoRoot -Recurse -File -Include '*.csproj', '*.props', '*.targets' |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            ForEach-Object {
                $file = $_
                $document = [System.Xml.Linq.XDocument]::Load($file.FullName, [System.Xml.Linq.LoadOptions]::SetLineInfo)
                $relative = $file.FullName.Substring($repoRoot.Length + 1)

                $nodes = @()
                $nodes += @($document.Descendants() | Where-Object { $_.Name.LocalName -eq 'PackagePath' })
                $nodes += @($document.Descendants().Attributes() | Where-Object { $_.Name.LocalName -eq 'PackagePath' })

                foreach ($node in $nodes) {
                    if ($node.Value -like '*\*') {
                        "${relative}:$(([System.Xml.IXmlLineInfo]$node).LineNumber) -> PackagePath=$($node.Value)"
                    }
                }
            }
    )

    Write-Host ''
    Write-Host 'PackagePath declarations'
    Assert-True ($backslashPaths.Count -eq 0) `
        'no PackagePath contains a backslash' `
        "offenders: $($backslashPaths -join ', ')"

    # --- Abstractions: guidance payload, manifest, and nothing that runs in a consumer ---------
    $abstractionsId = 'Trellis.ResourceNaming.Abstractions'
    $abstractionsPkg = Get-Nupkg $abstractionsId
    $entries = Get-Entries $abstractionsPkg
    $guidancePath = 'trellis-api-resourcenaming.md'

    Write-Host ''
    Write-Host "$abstractionsId ($(Split-Path -Leaf $abstractionsPkg))"

    Assert-True ($entries -contains $guidancePath) "packs $guidancePath" "entries: $($entries -join ', ')"
    Assert-True ($entries -contains 'guidance/reference-manifest.json') 'packs guidance/reference-manifest.json'

    $zip = [System.IO.Compression.ZipFile]::OpenRead($abstractionsPkg)
    try {
        $manifestEntry = $zip.GetEntry('guidance/reference-manifest.json')
        $docEntry = $zip.GetEntry($guidancePath)
        if ($manifestEntry -and $docEntry) {
            $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
            try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            $stream = $docEntry.Open()
            try {
                $memory = [System.IO.MemoryStream]::new()
                $stream.CopyTo($memory)
                $hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($memory.ToArray())).ToLowerInvariant()
            }
            finally { $stream.Dispose() }

            $document = @($manifest.documents)[0]
            $description = if ($document) { [string] $document.description } else { '' }
            Assert-True ($manifest.schemaVersion -eq 1 -and @($manifest.documents).Count -eq 1 -and
                $document.path -eq $guidancePath -and $document.sha256 -eq $hash -and $document.usage -eq 'onDemand' -and
                $description.Length -gt 0 -and $description.Length -le 200 -and $description -match '^Open when ' -and
                $null -eq $manifest.PSObject.Properties['entryPoints']) `
                'manifest matches the packed bytes, with onDemand usage and an "Open when ..." description' `
                "manifest: $($manifest | ConvertTo-Json -Compress -Depth 5)"
        }
    }
    finally { $zip.Dispose() }

    $runtimeEntries = @($entries | Where-Object { $_ -match '^(build|buildTransitive|trellis)/' })
    Assert-True ($runtimeEntries.Count -eq 0) `
        'packs no build/, buildTransitive/ or trellis/ entries (restore must never touch a consumer repository)' `
        "found: $($runtimeEntries -join ', ')"

    # XPath rather than property access: a package with no dependencies has no <dependencies> element, which
    # strict mode would turn into an error on exactly the package that is meant to pass.
    $abstractionsDeps = @((Get-Nuspec $abstractionsPkg).SelectNodes('//*[local-name()="dependency"]') |
        Where-Object { $_.GetAttribute('id') -match '^Trellis\.(AgentDocs|Core)' })
    Assert-True ($abstractionsDeps.Count -eq 0) `
        'does not depend on the packaging helper or Trellis.Core' `
        "found: $(($abstractionsDeps | ForEach-Object { $_.GetAttribute('id') }) -join ', ')"

    # --- Azure: ships no guidance of its own; consumers approve Abstractions only ------------------
    $azureId = 'Trellis.ResourceNaming.Azure'
    $azurePkg = Get-Nupkg $azureId
    $azureEntries = Get-Entries $azurePkg
    $nuspec = Get-Nuspec $azurePkg

    Write-Host ''
    Write-Host "$azureId ($(Split-Path -Leaf $azurePkg))"

    $azureGuidance = @($azureEntries | Where-Object { $_ -match '^(guidance|build|buildTransitive|trellis)/' -or $_ -like 'trellis-api-*.md' })
    Assert-True ($azureGuidance.Count -eq 0) `
        'ships no guidance manifest, guidance document or build assets of its own' `
        "found: $($azureGuidance -join ', ')"

    $dependency = @($nuspec.SelectNodes('//*[local-name()="dependency"]') |
        Where-Object { $_.GetAttribute('id') -eq $abstractionsId }) | Select-Object -First 1
    Assert-True ($null -ne $dependency) "declares a dependency on $abstractionsId, so restoring it makes the guidance available"

    # --- The published validator ------------------------------------------------------------------
    # Pinned to the packaging helper's version, which is published in lockstep with the tool.
    $abstractionsProject = [xml](Get-Content -LiteralPath (Join-Path $repoRoot 'src/Trellis.ResourceNaming.Abstractions/Trellis.ResourceNaming.Abstractions.csproj') -Raw)
    $toolVersion = $abstractionsProject.SelectSingleNode('//PackageReference[@Include="Trellis.AgentDocs.Packaging"]').GetAttribute('Version')
    $toolDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "rn-agentdocs-$([System.Guid]::NewGuid().ToString('N'))"
    try {
        $install = & dotnet tool install Trellis.AgentDocs --version $toolVersion --tool-path $toolDirectory 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Could not install Trellis.AgentDocs $toolVersion for validation:`n$($install | Out-String)" }
        $validation = & (Join-Path $toolDirectory 'agentdocs') validate $abstractionsPkg --strict 2>&1
        Assert-True ($LASTEXITCODE -eq 0) `
            "agentdocs validate --strict accepts the packed package ($toolVersion)" `
            ($validation | Out-String)
    }
    finally { Remove-Item -LiteralPath $toolDirectory -Recurse -Force -ErrorAction SilentlyContinue }

    # --- Both READMEs explain the opt-in ----------------------------------------------------------
    foreach ($id in @($abstractionsId, $azureId)) {
        $pkg = Get-Nupkg $id
        $zip = [System.IO.Compression.ZipFile]::OpenRead($pkg)
        try {
            $readmeEntry = $zip.GetEntry('README.md')
            $readmeText = ''
            if ($readmeEntry) {
                $readmeReader = [System.IO.StreamReader]::new($readmeEntry.Open())
                try { $readmeText = $readmeReader.ReadToEnd() } finally { $readmeReader.Dispose() }
            }
        }
        finally { $zip.Dispose() }

        Write-Host ''
        Write-Host "$id (README opt-in)"
        Assert-True ($readmeText -match "(?m)^dotnet tool install Trellis\.AgentDocs --version $([regex]::Escape($toolVersion)) --tool-manifest \.config/dotnet-tools\.json\r?$" -and
            $readmeText.Contains('dotnet tool run agentdocs init <solution-or-project>') -and
            $readmeText.Contains('approvedPackages') -and $readmeText.Contains('dotnet tool run agentdocs sync') -and
            $readmeText.Contains($abstractionsId)) `
            'README explains installing the tool, approving Trellis.ResourceNaming.Abstractions, and syncing'
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
