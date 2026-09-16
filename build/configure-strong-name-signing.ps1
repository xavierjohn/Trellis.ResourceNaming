[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:STRONG_NAME_KEY_BASE64)) {
    throw "GitHub Actions secret 'STRONG_NAME_KEY_BASE64' is missing or empty."
}

if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    throw "GitHub Actions did not provide RUNNER_TEMP."
}

if ([string]::IsNullOrWhiteSpace($env:GITHUB_ENV)) {
    throw "GitHub Actions did not provide GITHUB_ENV."
}

try {
    $keyBytes = [Convert]::FromBase64String($env:STRONG_NAME_KEY_BASE64.Trim())
}
catch [FormatException] {
    throw "GitHub Actions secret 'STRONG_NAME_KEY_BASE64' is not valid base64."
}

if ($keyBytes.Length -eq 0) {
    throw "GitHub Actions secret 'STRONG_NAME_KEY_BASE64' decoded to an empty key."
}

$keyPath = Join-Path $env:RUNNER_TEMP 'Trellis.ResourceNaming.snk'
[IO.File]::WriteAllBytes($keyPath, $keyBytes)
[Array]::Clear($keyBytes, 0, $keyBytes.Length)

if (-not $IsWindows) {
    [IO.File]::SetUnixFileMode(
        $keyPath,
        [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite)
}

"StrongNameKeyFile=$keyPath" | Add-Content -LiteralPath $env:GITHUB_ENV -Encoding utf8
"RequireFullStrongNameSigning=true" | Add-Content -LiteralPath $env:GITHUB_ENV -Encoding utf8

Write-Host "Configured full strong-name signing for trusted builds."
