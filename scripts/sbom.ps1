<#
.SYNOPSIS
  Writes the software bill of materials (SBOM) of the server release: what third-party components it ships.

.DESCRIPTION
  Output is one CycloneDX JSON file, artifacts/sbom/MacroGrid-Server-<version>.cdx.json. It is built from four parts
  (kept in artifacts/sbom/parts/, not attached to the release) and merged, a package that several parts share appearing once:
    dotnet    the .NET packages of MacroGrid.exe (the host and every project it references; test projects and
              development-only packages are left out)
    editor    the npm packages bundled into the editor
    deck      the npm packages bundled into the browser deck
    renderer  the npm packages of the shared renderer that the editor and the deck both bundle from source
  release.yml attaches the merged file to the release. The generators are pinned below and run from the package registries;
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

$partsDir = Join-Path $OutDir "parts"
if (Test-Path $partsDir) { Remove-Item $partsDir -Recurse -Force }
New-Item -ItemType Directory -Force $partsDir | Out-Null
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
    --output $partsDir --filename "$prefix-dotnet.cdx.json"
if ($LASTEXITCODE) { throw "The .NET SBOM failed" }

# npm: production dependencies only, read from the lockfile. Reading node_modules instead would walk into the renderer,
# which is linked from packages/renderer, and list its development tools too; the renderer part covers its own
# dependencies. --ignore-npm-errors because npm reports the linked renderer's dependencies as missing.
$npmParts = [ordered]@{ editor = "editor"; deck = "webclient"; renderer = "packages\renderer" }
foreach ($part in $npmParts.Keys) {
    Push-Location (Join-Path $root $npmParts[$part])
    try {
        npx --yes "@cyclonedx/cyclonedx-npm@$npmGeneratorVersion" --omit dev --ignore-npm-errors --package-lock-only `
            --output-format JSON --output-file (Join-Path $partsDir "$prefix-$part.cdx.json")
        if ($LASTEXITCODE) { throw "The $part SBOM failed" }
    } finally { Pop-Location }
}

# One file for the release: the parts' components under one root, every package once (matched by its package URL),
# and each part's own dependency links kept. References are prefixed with the part name so they cannot collide.
$rootRef = "macro-grid-server@$version"
$components = [System.Collections.Generic.List[object]]::new()
$byPurl = @{}
$refMap = @{}
$links = [ordered]@{}
$partRoots = @()
$specVersion = $null

function Add-Ref($component, [string] $part) {
    if ($component.'bom-ref') { $component.'bom-ref' = "${part}:$($component.'bom-ref')" }
    if ($component.components) { foreach ($child in $component.components) { Add-Ref $child $part } }
}

foreach ($file in Get-ChildItem $partsDir -Filter "$prefix-*.cdx.json" | Sort-Object Name) {
    $part = $file.BaseName.Substring($prefix.Length + 1) -replace '\.cdx$', ''
    $bom = Get-Content $file.FullName -Raw | ConvertFrom-Json
    # The generators write different CycloneDX versions (1.6 and 1.7); each newer version only adds to the older, so the highest is used.
    if (-not $specVersion -or [version]$bom.specVersion -gt [version]$specVersion) { $specVersion = $bom.specVersion }

    # The part's own root component becomes an ordinary component that the merged root depends on.
    $root = $bom.metadata.component
    if ($root) {
        $rootOldRef = $root.'bom-ref'
        $root.name = "$($root.name) ($part)"
        Add-Ref $root $part
        $components.Add($root)
        $partRoots += $root.'bom-ref'
        if ($rootOldRef) { $refMap["${part}:$rootOldRef"] = $root.'bom-ref' }
    }
    foreach ($component in ($bom.components | Where-Object { $_ })) {
        $oldRef = $component.'bom-ref'
        Add-Ref $component $part
        $purl = $component.purl
        if ($purl -and $byPurl.ContainsKey($purl)) {
            if ($oldRef) { $refMap["${part}:$oldRef"] = $byPurl[$purl] }
            continue
        }
        if ($purl) { $byPurl[$purl] = $component.'bom-ref' }
        if ($oldRef) { $refMap["${part}:$oldRef"] = $component.'bom-ref' }
        $components.Add($component)
    }
    foreach ($dependency in ($bom.dependencies | Where-Object { $_ })) {
        $ref = $refMap["${part}:$($dependency.ref)"]
        if (-not $ref) { $ref = "${part}:$($dependency.ref)" }
        if (-not $links.Contains($ref)) { $links[$ref] = [System.Collections.Generic.List[string]]::new() }
        foreach ($target in ($dependency.dependsOn | Where-Object { $_ })) {
            $mapped = $refMap["${part}:$target"]
            if (-not $mapped) { $mapped = "${part}:$target" }
            if (-not $links[$ref].Contains($mapped)) { $links[$ref].Add($mapped) }
        }
    }
}

$dependencies = @([ordered]@{ ref = $rootRef; dependsOn = @($partRoots) })
foreach ($ref in $links.Keys) { $dependencies += [ordered]@{ ref = $ref; dependsOn = @($links[$ref]) } }

$merged = [ordered]@{
    bomFormat    = "CycloneDX"
    specVersion  = $specVersion
    serialNumber = "urn:uuid:$([guid]::NewGuid())"
    version      = 1
    metadata     = [ordered]@{
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
        component = [ordered]@{ type = "application"; "bom-ref" = $rootRef; name = "Macro Grid server"; version = $version }
    }
    components   = @($components)
    dependencies = $dependencies
}
$mergedFile = Join-Path $OutDir "$prefix.cdx.json"
[System.IO.File]::WriteAllText($mergedFile, ($merged | ConvertTo-Json -Depth 100), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "SBOM: $(Split-Path -Leaf $mergedFile) ($($components.Count) components from $($partRoots.Count) parts)"
