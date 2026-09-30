<#
.SYNOPSIS
  Builds the release folder of the server: the editor and browser deck bundles, then a self-contained single-file MacroGrid.exe.

.DESCRIPTION
  Output goes to artifacts/server/. The version comes from <Version> in Directory.Build.props, the single place the
  server version lives (docs/guides/versioning.md). Run installer\build-installer.ps1 afterwards to wrap the
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

$props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
if ($props -notmatch '<Version>(\d+\.\d+\.\d+)</Version>') { throw "Could not read <Version> (MAJOR.MINOR.PATCH) from Directory.Build.props" }
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

    # The Host serves the editor from its own wwwroot/editor folder. Every asset filename carries a
    # content hash, so an old build's files are never overwritten by a new one — only removing the
    # folder first keeps a stale index-*.js from earlier builds out of the published output.
    $target = Join-Path $root "src\MacroGrid.Host\wwwroot\editor"
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
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
    if (Test-Path $deck) { Remove-Item $deck -Recurse -Force }
    New-Item -ItemType Directory -Force $deck | Out-Null
    Copy-Item (Join-Path $root "webclient\dist\*") $deck -Recurse -Force
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish (Join-Path $root "src\MacroGrid.Host") -c $Configuration -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -p:Version=$version -o $out
if ($LASTEXITCODE) { throw "dotnet publish failed" }

# Reference documentation the WebView2 package drops next to the exe; not needed at runtime.
Get-ChildItem $out -Filter "*.xml" | Remove-Item -Force

Copy-Item (Join-Path $root "LICENSE") $out
Copy-Item (Join-Path $root "THIRD_PARTY_NOTICES.md") $out
# The agreement the installer asks the person to accept; the Help window shows it too.
Copy-Item (Join-Path $root "installer\license-agreement.txt") $out
# Original license texts of every third-party library (indexed by THIRD_PARTY_NOTICES.md).
Copy-Item (Join-Path $root "licenses") (Join-Path $out "licenses") -Recurse -Force
Set-Content (Join-Path $out "version.txt") $version -NoNewline

# Guardrail (docs/plans/faster-install-plan.md): the published folder is what the installer copies file by file,
# so a regression here (a stale build, a bundler config that stops chunking icons) directly slows every install
# and every auto-update. Fails loudly instead of silently shipping thousands of files again.
$fileCount = (Get-ChildItem $out -Recurse -File).Count
Write-Host "Published files: $fileCount"
$maxFiles = 100
if ($fileCount -gt $maxFiles) {
    throw "Published output has $fileCount files (limit $maxFiles). This usually means a build folder was not cleaned, or a bundler stopped chunking assets into a handful of files - see docs/plans/faster-install-plan.md."
}
foreach ($assets in @("editor", "deck")) {
    $assetsDir = Join-Path $out "wwwroot\$assets\assets"
    if (Test-Path $assetsDir) {
        $indexFiles = Get-ChildItem $assetsDir -Filter "index-*.js"
        if ($indexFiles.Count -gt 1) {
            throw "wwwroot\$assets\assets has $($indexFiles.Count) index-*.js files; expected 1. A previous build's output was not cleaned before this one - see docs/plans/faster-install-plan.md."
        }
    }
}

Write-Host "Done: $out"
