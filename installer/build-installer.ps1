<#
.SYNOPSIS
  Wraps artifacts/server (from scripts\publish.ps1) into artifacts/MacroStation-Setup-<version>.exe with Inno Setup.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "artifacts\server"
if (-not (Test-Path (Join-Path $publish "MacroStation.exe"))) { throw "Run scripts\publish.ps1 first." }
$version = (Get-Content (Join-Path $publish "version.txt") -Raw).Trim()

$candidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }
if (-not $candidates) { throw "Inno Setup 6 was not found. Install it from https://jrsoftware.org/isinfo.php and run this again." }

& ($candidates | Select-Object -First 1) "/DAppVersion=$version" "/DSourceDir=$publish" "/DOutputDir=$(Join-Path $root 'artifacts')" (Join-Path $PSScriptRoot "MacroStation.iss")
if ($LASTEXITCODE) { throw "Inno Setup failed" }
