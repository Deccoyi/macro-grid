<#
.SYNOPSIS
  Creates or updates the GitHub labels listed in .github\labels.json.
.DESCRIPTION
  Dry run by default: it only prints what it would create or change. Pass -Apply to do it.
  It never deletes a label and never renames one; labels that exist on GitHub but are not in the file are only counted.
  Needs the GitHub CLI (gh) signed in with the "repo" scope. Safe to run again.
.PARAMETER Repo
  owner/name. Defaults to the repository of the current folder.
#>
param(
  [string]$Repo,
  [switch]$Apply
)

$ErrorActionPreference = 'Stop'

$gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
if (-not $gh) { $gh = Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe' }
if (-not (Test-Path $gh)) { throw 'The GitHub CLI (gh) was not found.' }

if (-not $Repo) { $Repo = (& $gh repo view --json nameWithOwner --jq .nameWithOwner).Trim() }

# Windows PowerShell 5.1 returns a parsed JSON array as one object; this hands its items out one by one.
function ConvertFrom-JsonArray([string]$Text) {
  $items = ConvertFrom-Json $Text
  foreach ($item in $items) { $item }
}

$root = Split-Path -Parent $PSScriptRoot
$wanted = @(ConvertFrom-JsonArray (Get-Content (Join-Path $root '.github\labels.json') -Raw))
$existing = @(ConvertFrom-JsonArray ((& $gh label list -R $Repo --limit 300 --json name,color,description) -join "`n"))

$byName = @{}
foreach ($label in $existing) { $byName[$label.name.ToLowerInvariant()] = $label }

$created = 0; $changed = 0; $same = 0
foreach ($label in $wanted) {
  $current = $byName[$label.name.ToLowerInvariant()]
  if (-not $current) {
    Write-Host "create  $($label.name)"
    if ($Apply) { & $gh label create $label.name --color $label.color --description $label.description -R $Repo | Out-Null }
    $created++
  }
  elseif ($current.color -ne $label.color -or $current.description -ne $label.description) {
    Write-Host "update  $($label.name)"
    if ($Apply) { & $gh label edit $label.name --color $label.color --description $label.description -R $Repo | Out-Null }
    $changed++
  }
  else { $same++ }
}

$listed = @{}
foreach ($label in $wanted) { $listed[$label.name.ToLowerInvariant()] = $true }
$unlisted = @($existing | Where-Object { -not $listed[$_.name.ToLowerInvariant()] }).Count

$mode = if ($Apply) { 'applied' } else { 'dry run, nothing changed (use -Apply)' }
Write-Host "${Repo}: $created to create, $changed to update, $same unchanged, $unlisted on GitHub but not in the file (left alone). [$mode]"
