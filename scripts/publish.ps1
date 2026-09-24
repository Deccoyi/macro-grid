<#
.SYNOPSIS
  Builds the release folder of the server: the editor and browser deck bundles, then a self-contained single-file MacroGrid.exe.

.DESCRIPTION
  Output goes to artifacts/server/. The version comes from ClientHub.ServerVersion, the single place the
  server version lives (docs/versioning.md). Run installer\build-installer.ps1 afterwards to wrap the
  folder in a Windows installer.

  The runtime and ASP.NET are bundled, so the target PC needs nothing installed except the WebView2
  Runtime (present on Windows 11 and on current Windows 10 updates).
#>
param(
    [string] $Configuration = "Release",
    [switch] $SkipEditor
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "artifacts\server"

$hub = Get-Content (Join-Path $root "src\MacroGrid.Core\Sessions\ClientHub.cs") -Raw
if ($hub -notmatch 'ServerVersion\s*=\s*"([^"]+)"') { throw "Could not read ServerVersion from ClientHub.cs" }
$version = $Matches[1]
Write-Host "Macro Grid server $version"

if (-not $SkipEditor) {
    # The editor and the deck import @macro/renderer from packages/renderer, whose own dependencies (postcss, ...)
    # must be installed too, or the bundle build cannot resolve them on a clean checkout.
    Push-Location (Join-Path $root "packages/renderer")
    try {
        if (-not (Test-Path "node_modules")) { npm ci; if ($LASTEXITCODE) { throw "npm ci failed (renderer)" } }
    } finally { Pop-Location }

    Push-Location (Join-Path $root "editor")
    try {
        if (-not (Test-Path "node_modules")) { npm ci; if ($LASTEXITCODE) { throw "npm ci failed" } }
        npm run build
        if ($LASTEXITCODE) { throw "Editor build failed" }
    } finally { Pop-Location }

    # The Host serves the editor from its own wwwroot/editor folder.
    $target = Join-Path $root "src\MacroGrid.Host\wwwroot\editor"
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item (Join-Path $root "editor\dist\*") $target -Recurse -Force

    # The browser deck (webclient/) is served at /deck/ from wwwroot/deck.
    Push-Location (Join-Path $root "webclient")
    try {
        if (-not (Test-Path "node_modules")) { npm ci; if ($LASTEXITCODE) { throw "npm ci failed (webclient)" } }
        npm run build
        if ($LASTEXITCODE) { throw "Browser deck build failed" }
    } finally { Pop-Location }
    $deck = Join-Path $root "src\MacroGrid.Host\wwwroot\deck"
    New-Item -ItemType Directory -Force $deck | Out-Null
    Copy-Item (Join-Path $root "webclient\dist\*") $deck -Recurse -Force
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish (Join-Path $root "src\MacroGrid.Host") -c $Configuration -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -p:DebugSymbols=false -p:Version=$version -o $out
if ($LASTEXITCODE) { throw "dotnet publish failed" }

# Reference documentation the WebView2 package drops next to the exe; not needed at runtime.
Get-ChildItem $out -Filter "*.xml" | Remove-Item -Force

Copy-Item (Join-Path $root "LICENSE") $out
Copy-Item (Join-Path $root "THIRD_PARTY_NOTICES.md") $out
# Original license texts of every third-party library (indexed by THIRD_PARTY_NOTICES.md).
Copy-Item (Join-Path $root "licenses") (Join-Path $out "licenses") -Recurse -Force
Set-Content (Join-Path $out "version.txt") $version -NoNewline
Write-Host "Done: $out"
