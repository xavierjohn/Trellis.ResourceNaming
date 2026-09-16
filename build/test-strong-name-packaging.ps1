#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PackageDirectory,

    [switch] $RequireFullSignature
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-StrongNameVerifier {
    param(
        [Parameter(Mandatory)]
        [string] $VerifierPath,

        [Parameter(Mandatory)]
        [string] $AssemblyPath
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $VerifierPath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add('-q')
    $startInfo.ArgumentList.Add('-vf')
    $startInfo.ArgumentList.Add($AssemblyPath)

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "Failed to start strong-name verifier '$VerifierPath'."
        }

        $standardOutput = $process.StandardOutput.ReadToEndAsync()
        $standardError = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()

        $output = $standardOutput.GetAwaiter().GetResult()
        $errorOutput = $standardError.GetAwaiter().GetResult()
        [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = @($output.Trim(), $errorOutput.Trim()) |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        }
    }
    finally {
        $process.Dispose()
    }
}

$expectedPublicKeyToken = '30edd03a0eb2b9d7'
$expectedAssemblies = @(
    'Trellis.ResourceNaming.Abstractions',
    'Trellis.ResourceNaming.Azure'
)
$expectedPublicKeyPath = Join-Path $PSScriptRoot 'Trellis.ResourceNaming.PublicKey.snk'
$expectedPublicKey = [Convert]::ToBase64String(
    [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $expectedPublicKeyPath).Path))

$strongNameVerifier = $null
if ($RequireFullSignature) {
    $strongNameVerifier = Get-Command sn -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $strongNameVerifier) {
        throw "The 'sn' strong-name verifier is required for cryptographic signature validation."
    }
}

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

                $actualPublicKey = $identity.GetPublicKey()
                if ($null -eq $actualPublicKey -or
                    [Convert]::ToBase64String($actualPublicKey) -cne $expectedPublicKey) {
                    throw "$assemblyName does not contain the expected strong-name public key."
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
                        $signatureSection = $peReader.PEHeaders.SectionHeaders |
                            Where-Object {
                                $signatureDirectory.RelativeVirtualAddress -ge $_.VirtualAddress -and
                                $signatureDirectory.RelativeVirtualAddress -lt
                                    ($_.VirtualAddress + $_.SizeOfRawData)
                            } |
                            Select-Object -First 1

                        if ($null -eq $signatureSection) {
                            throw "$assemblyName has an invalid strong-name signature location."
                        }

                        $signatureFileOffset = $signatureSection.PointerToRawData +
                            ($signatureDirectory.RelativeVirtualAddress - $signatureSection.VirtualAddress)
                    }
                }
                finally {
                    $peReader.Dispose()
                    $assemblyStream.Dispose()
                }

                if ($RequireFullSignature) {
                    $verification = Invoke-StrongNameVerifier $strongNameVerifier.Source $assemblyPath
                    if ($verification.ExitCode -ne 0) {
                        $detail = $verification.Output -join [Environment]::NewLine
                        throw "$assemblyName failed cryptographic strong-name verification (exit $($verification.ExitCode)).`n$detail"
                    }

                    $tamperedAssemblyPath = Join-Path $inspectionDirectory "$assemblyName.tampered.dll"
                    Copy-Item -LiteralPath $assemblyPath -Destination $tamperedAssemblyPath
                    $tamperedAssembly = [IO.File]::Open(
                        $tamperedAssemblyPath,
                        [IO.FileMode]::Open,
                        [IO.FileAccess]::ReadWrite,
                        [IO.FileShare]::None)
                    try {
                        $tamperedAssembly.Position = $signatureFileOffset
                        $signatureByte = $tamperedAssembly.ReadByte()
                        if ($signatureByte -lt 0) {
                            throw "$assemblyName has an invalid strong-name signature offset."
                        }
                        $tamperedAssembly.Position = $signatureFileOffset
                        $tamperedAssembly.WriteByte([byte]($signatureByte -bxor 1))
                    }
                    finally {
                        $tamperedAssembly.Dispose()
                    }

                    $tamperedVerification = Invoke-StrongNameVerifier `
                        $strongNameVerifier.Source `
                        $tamperedAssemblyPath
                    if ($tamperedVerification.ExitCode -eq 0) {
                        throw "The strong-name verifier accepted a deliberately corrupted signature for $assemblyName."
                    }
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
