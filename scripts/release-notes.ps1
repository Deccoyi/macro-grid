<#
.SYNOPSIS
  Prints the release notes of a server release: the section of docs/CHANGELOG.md for the version in the tag.

.DESCRIPTION
  The update window in the app shows the body of each GitHub release, so the body has to be the short public notes and not a placeholder.
  The tag "server-v0.3.0-alpha" is looked up as "## 0.3.0" (the version without the label). When the changelog has no such section,
  the script prints a short pointer to the changelog instead, so a draft is never empty.
#>
param(
    [Parameter(Mandatory = $true)] [string] $Tag,
    [string] $Changelog
)

$ErrorActionPreference = "Stop"
if (-not $Changelog) { $Changelog = Join-Path (Split-Path -Parent $PSScriptRoot) "docs\CHANGELOG.md" }

if ($Tag -notmatch '^server-v(\d+\.\d+\.\d+)') { throw "The tag '$Tag' is not a server release tag (server-vX.Y.Z)." }
$version = $Matches[1]

$lines = Get-Content $Changelog
$start = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match "^##\s+\[?$([regex]::Escape($version))\]?(\s|$)") { $start = $i + 1; break }
}

if ($start -lt 0) {
    "See docs/CHANGELOG.md for what changed in $version."
    return
}

$section = @()
for ($i = $start; $i -lt $lines.Count -and $lines[$i] -notmatch '^##\s'; $i++) { $section += $lines[$i] }
($section -join "`n").Trim()
