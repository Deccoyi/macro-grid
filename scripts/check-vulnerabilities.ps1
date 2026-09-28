<#
.SYNOPSIS
  Fails when a dependency that ships with the server has a known vulnerability.

.DESCRIPTION
  Checks the .NET packages of the whole solution (including transitive ones) against the NuGet vulnerability data,
  and the production npm packages of the editor, the browser deck and the shared renderer with npm audit (moderate
  and above). Development-only npm packages are not shipped and not checked. CI runs this on every pull request and
  release.yml runs it before building a release, so a release never knowingly ships a vulnerable component.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = @()

Push-Location $root
try {
    dotnet restore MacroGrid.slnx --verbosity quiet
    if ($LASTEXITCODE) { throw "dotnet restore failed" }
    $report = dotnet list MacroGrid.slnx package --vulnerable --include-transitive 2>&1 | Out-String
    if ($LASTEXITCODE) { Write-Host $report; throw "dotnet list package failed" }
    Write-Host $report
    if ($report -match "has the following vulnerable packages") { $failed += ".NET packages" }
} finally { Pop-Location }

foreach ($dir in @("editor", "webclient", "packages/renderer")) {
    Push-Location (Join-Path $root $dir)
    try {
        Write-Host "npm audit: $dir"
        npm audit --omit=dev --audit-level=moderate
        if ($LASTEXITCODE) { $failed += "npm packages in $dir" }
    } finally { Pop-Location }
}

if ($failed.Count) { throw "Known vulnerabilities in: $($failed -join ', '). Update or replace them first." }
Write-Host "No known vulnerabilities in the shipped dependencies."
