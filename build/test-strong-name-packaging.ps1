#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PackageDirectory,

    [switch] $RequireFullSignature
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedPublicKeyToken = '30edd03a0eb2b9d7'
$expectedAssemblies = @(
    'Trellis.ResourceNaming.Abstractions',
    'Trellis.ResourceNaming.Azure'
)

$packagePath = (Resolve-Path -LiteralPath $PackageDirectory).Path
$packages = @(
    Get-ChildItem -LiteralPath $packagePath -Filter '*.nupkg' -File |
        Where-Object { $_.Name -notlike '*.symbols.nupkg' }
)

if ($packages.Count -eq 0) {
    throw "No .nupkg files found in '$packagePath'."
}

$inspectionDirectory = Join-Path ([IO.Path]::GetTempPath()) "rn-strong-name-$([Guid]::NewGuid().ToString('N'))"
$verifiedAssemblies = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

try {
    New-Item -ItemType Directory -Path $inspectionDirectory | Out-Null

    foreach ($package in $packages) {
        $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            foreach ($assemblyName in $expectedAssemblies) {
                $entry = $archive.Entries |
                    Where-Object {
                        $_.FullName -like "lib/*/$assemblyName.dll" -and
                        $_.Name -eq "$assemblyName.dll"
                    } |
                    Select-Object -First 1

                if ($null -eq $entry) {
                    continue
                }

                $assemblyPath = Join-Path $inspectionDirectory "$assemblyName.dll"
                $source = $entry.Open()
                $destination = [IO.File]::Create($assemblyPath)
                try {
                    $source.CopyTo($destination)
                }
                finally {
                    $destination.Dispose()
                    $source.Dispose()
                }

                $identity = [System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
                $publicKeyToken = $identity.GetPublicKeyToken()
                $actualPublicKeyToken = if ($null -ne $publicKeyToken -and $publicKeyToken.Length -gt 0) {
                    [Convert]::ToHexString($publicKeyToken).ToLowerInvariant()
                }
                else {
                    ''
                }

                if ($actualPublicKeyToken -ne $expectedPublicKeyToken) {
                    throw "$assemblyName has public key token '$actualPublicKeyToken'; expected '$expectedPublicKeyToken'."
                }

                $assemblyStream = [IO.File]::OpenRead($assemblyPath)
                $peReader = [System.Reflection.PortableExecutable.PEReader]::new($assemblyStream)
                try {
                    $corHeader = $peReader.PEHeaders.CorHeader
                    if ($null -eq $corHeader) {
                        throw "$assemblyName is not a managed assembly."
                    }

                    if (-not $corHeader.Flags.HasFlag(
                            [System.Reflection.PortableExecutable.CorFlags]::StrongNameSigned)) {
                        throw "$assemblyName does not carry the StrongNameSigned flag."
                    }

                    $signatureDirectory = $corHeader.StrongNameSignatureDirectory
                    if ($signatureDirectory.Size -le 0) {
                        throw "$assemblyName has no strong-name signature directory."
                    }

                    if ($RequireFullSignature) {
                        $sectionData = $peReader.GetSectionData($signatureDirectory.RelativeVirtualAddress)
                        $signature = [byte[]]$sectionData.GetContent(0, $signatureDirectory.Size)

                        if (-not ($signature | Where-Object { $_ -ne 0 } | Select-Object -First 1)) {
                            throw "$assemblyName is public-signed only; a full strong-name signature is required."
                        }
                    }
                }
                finally {
                    $peReader.Dispose()
                    $assemblyStream.Dispose()
                }

                $null = $verifiedAssemblies.Add($assemblyName)
                Write-Host "Verified $assemblyName ($actualPublicKeyToken) in $($package.Name)."
            }
        }
        finally {
            $archive.Dispose()
        }
    }

    $missingAssemblies = @($expectedAssemblies | Where-Object { -not $verifiedAssemblies.Contains($_) })
    if ($missingAssemblies.Count -gt 0) {
        throw "The packages did not contain: $($missingAssemblies -join ', ')."
    }

    $signatureKind = if ($RequireFullSignature) { 'full signatures' } else { 'strong-name identities' }
    Write-Host "Verified $signatureKind for all shipping assemblies."
}
finally {
    if (Test-Path -LiteralPath $inspectionDirectory) {
        Remove-Item -LiteralPath $inspectionDirectory -Recurse -Force
    }
}
