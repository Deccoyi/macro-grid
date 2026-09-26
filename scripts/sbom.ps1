<#
.SYNOPSIS
  Writes the software bill of materials (SBOM) of the server release: what third-party components it ships.

.DESCRIPTION
  Output goes to artifacts/sbom/, one CycloneDX JSON file per part, named MacroGrid-Server-<version>-<part>.cdx.json:
    dotnet    the .NET packages of MacroGrid.exe (the host and every project it references; test projects and
              development-only packages are left out)
    editor    the npm packages bundled into the editor
    deck      the npm packages bundled into the browser deck
    renderer  the npm packages of the shared renderer that the editor and the deck both bundle from source
  release.yml attaches them to the release. The generators are pinned below and run from the package registries;
  nothing is added to the repository. The npm parts are read from each package-lock.json, so no install is needed.
#>
param(
    [string] $OutDir
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $root "artifacts\sbom" }

# Pinned so that a release is reproducible; update deliberately.
$dotnetGeneratorVersion = "6.2.0"
$npmGeneratorVersion = "6.0.1"

$props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
if ($props -notmatch '<Version>(\d+\.\d+\.\d+)</Version>') { throw "Could not read <Version> (MAJOR.MINOR.PATCH) from Directory.Build.props" }
$version = $Matches[1]

New-Item -ItemType Directory -Force $OutDir | Out-Null
$prefix = "MacroGrid-Server-$version"

# .NET: the generator is installed into artifacts/tools, not globally. The explicit source is needed because the
# repository's nuget.config clears the sources for everything but restore.
$tools = Join-Path $root "artifacts\tools"
$generator = Join-Path $tools "dotnet-CycloneDX.exe"
if (-not (Test-Path $generator)) {
    dotnet tool install CycloneDX --version $dotnetGeneratorVersion --tool-path $tools --add-source https://api.nuget.org/v3/index.json
    if ($LASTEXITCODE) { throw "Installing the .NET SBOM generator failed" }
    $generator = (Get-ChildItem $tools -Filter "dotnet-CycloneDX*" | Select-Object -First 1).FullName
}
& $generator (Join-Path $root "src\MacroGrid.Host\MacroGrid.Host.csproj") `
    --recursive --exclude-dev --exclude-test-projects --output-format Json `
    --set-name "Macro Grid server" --set-version $version `
    --output $OutDir --filename "$prefix-dotnet.cdx.json"
if ($LASTEXITCODE) { throw "The .NET SBOM failed" }

# npm: production dependencies only, read from the lockfile. Reading node_modules instead would walk into the renderer,
# which is linked from packages/renderer, and list its development tools too; the renderer part covers its own
# dependencies. --ignore-npm-errors because npm reports the linked renderer's dependencies as missing.
$npmParts = [ordered]@{ editor = "editor"; deck = "webclient"; renderer = "packages\renderer" }
foreach ($part in $npmParts.Keys) {
    Push-Location (Join-Path $root $npmParts[$part])
    try {
        npx --yes "@cyclonedx/cyclonedx-npm@$npmGeneratorVersion" --omit dev --ignore-npm-errors --package-lock-only `
            --output-format JSON --output-file (Join-Path $OutDir "$prefix-$part.cdx.json")
        if ($LASTEXITCODE) { throw "The $part SBOM failed" }
    } finally { Pop-Location }
}

Get-ChildItem $OutDir -Filter "$prefix-*.cdx.json" | ForEach-Object { Write-Host "SBOM: $($_.Name)" }
