<#
.SYNOPSIS
  Wraps artifacts/server (from scripts\publish.ps1) into artifacts/MacroGrid-Setup-<version>.exe with Inno Setup.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "artifacts\server"
if (-not (Test-Path (Join-Path $publish "MacroGrid.exe"))) { throw "Run scripts\publish.ps1 first." }
$version = (Get-Content (Join-Path $publish "version.txt") -Raw).Trim()

$candidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }
if (-not $candidates) { throw "Inno Setup 6 was not found. Install it from https://jrsoftware.org/isinfo.php and run this again." }

# Microsoft's WebView2 "Evergreen bootstrapper" (about 1.7 MB). The installer runs it only on PCs that lack the WebView2 Runtime.
# It is downloaded from Microsoft's official link on the first build and kept in artifacts\redist (git-ignored, never committed).
$redist = Join-Path $root "artifacts\redist"
$bootstrapper = Join-Path $redist "MicrosoftEdgeWebview2Setup.exe"
if (-not (Test-Path $bootstrapper)) {
    New-Item -ItemType Directory -Force $redist | Out-Null
    Write-Host "Downloading the WebView2 bootstrapper from Microsoft..."
    Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $bootstrapper -UseBasicParsing
}
$signature = Get-AuthenticodeSignature $bootstrapper
if ($signature.Status -ne "Valid" -or $signature.SignerCertificate.Subject -notmatch "O=Microsoft Corporation") {
    Remove-Item $bootstrapper -Force
    throw "The WebView2 bootstrapper is not signed by Microsoft (status $($signature.Status)); it was deleted."
}

& ($candidates | Select-Object -First 1) "/DAppVersion=$version" "/DSourceDir=$publish" "/DOutputDir=$(Join-Path $root 'artifacts')" "/DWebView2Setup=$bootstrapper" (Join-Path $PSScriptRoot "MacroGrid.iss")
if ($LASTEXITCODE) { throw "Inno Setup failed" }
