<#
.SYNOPSIS
  Builds the plugin tool (check, package, start and try out a JavaScript plugin) as a self-contained single-file macrogrid-plugin.exe.

.DESCRIPTION
  Output goes to artifacts/plugin-tool/, next to nothing else: the server's folder (artifacts/server/) is never touched. The version comes
  from <Version> in Directory.Build.props like everything else (docs/guides/versioning.md). The release workflow zips the folder and attaches
  it to the draft release; nothing here publishes anything.
#>
param(
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "artifacts\plugin-tool"

$props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
if ($props -notmatch '<Version>(\d+\.\d+\.\d+)</Version>') { throw "Could not read <Version> (MAJOR.MINOR.PATCH) from Directory.Build.props" }
$version = $Matches[1]
Write-Host "Macro Grid plugin tool $version"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish (Join-Path $root "tools\MacroGrid.PluginTool") -c $Configuration -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -p:Version=$version -o $out
if ($LASTEXITCODE) { throw "dotnet publish failed" }

Get-ChildItem $out -Filter "*.xml" | Remove-Item -Force

Copy-Item (Join-Path $root "LICENSE") $out
Copy-Item (Join-Path $root "THIRD_PARTY_NOTICES.md") $out
Copy-Item (Join-Path $root "licenses") (Join-Path $out "licenses") -Recurse -Force
$readme = Join-Path $root "tools\MacroGrid.PluginTool\README.md"
if (Test-Path $readme) { Copy-Item $readme $out }
Set-Content (Join-Path $out "version.txt") $version -NoNewline

$size = [math]::Round(((Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 1)
Write-Host "Done: $out ($size MB)"
