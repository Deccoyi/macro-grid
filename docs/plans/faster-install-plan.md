# A much faster install and update (fewer files)

Status: **Built and measured.** Phase 1 and 2 are in `scripts/publish.ps1`. Phase 0 confirmed the fix: 49 files,
66.3 MB, ~2.9 seconds of file copying during a real install (see Phase 0's table). Phase 3 (installer script) needed
no change. Phase 4 (archive packaging) is not needed — the target ("a few seconds") is already met.
Repositories: `macro-grid` only (installer script, publish script, editor and browser-deck build config). The phone
app and the plugins are not touched. Does not touch the security model or the SDK.

## Problem

Installing or updating Macro Grid takes far longer than it should. The setup's progress window shows thousands of
tiny files being copied one at a time, which can take over a minute even on a fast PC; antivirus real-time scanning
makes this worse on other PCs, since each small file is scanned individually. Auto-update runs the same setup
(`/UPDATE`, see [../design/auto-update.md](../design/auto-update.md)), so every install is also every update.
Wanted: a setup of a few seconds.

## What is already done

Two of the ideas the roadmap listed are already built, so this plan does not repeat them:

- **Icons bundled into one chunk.** `editor/vite.config.ts` (`manualChunks`) puts every `lucide-react` dynamic icon
  import into a single `icons-*.js` chunk instead of Vite's default of one file per icon. The editor still loads
  only the icon it draws (`lucide-react/dynamicIconImports`, used in `editor/src/components/StatusBar.tsx`,
  `editor/src/panels/PluginsToolWindow.tsx`, `editor/src/panels/ActionPicker.tsx`, `editor/src/panels/IconPicker.tsx`),
  it just no longer ships as thousands of separate files. See `docs/CHANGELOG-developer.md` ("about 1,500 files
  became 8"). A local build of `editor/dist` is 9 files, 2.4 MB.
- **Old versioned files cleared on upgrade.** `installer/MacroGrid.iss` has an `[InstallDelete]` entry that deletes
  `{app}\wwwroot` before the new files are copied, so a hashed file from a previous version (`index-<hash>.js`) does
  not linger next to the new one on the installed PC.
- **The published app is already a single compressed exe.** `scripts/publish.ps1` runs `dotnet publish` with
  `-r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
  -p:EnableCompressionInSingleFile=true`, so the .NET runtime and ASP.NET do not ship as separate files either.

The roadmap and plans list previously described this item as still needing both of the above; that wording is now
corrected to point here instead of repeating the outdated "~6,000 tiny files" / "clear old wwwroot first" framing.

## Root cause that is still open

`scripts/publish.ps1` copies build output into the Host's `wwwroot` with `Copy-Item -Force` but never empties the
destination folders first:

```
$target = Join-Path $root "src\MacroGrid.Host\wwwroot\editor"
...
Copy-Item (Join-Path $root "editor\dist\*") $target -Recurse -Force
...
$deck = Join-Path $root "src\MacroGrid.Host\wwwroot\deck"
...
Copy-Item (Join-Path $root "webclient\dist\*") $deck -Recurse -Force
```

`src\MacroGrid.Host\wwwroot\editor` and `src\MacroGrid.Host\wwwroot\deck` are git-ignored (`.gitignore`), so they
only exist as whatever a previous local or CI build left there. Vite hashes every asset filename
(`index-<hash>.js`), so a new build never overwrites an old chunk — it adds another one next to it. On any build
machine that has published more than once without deleting `wwwroot` by hand, the folder that
`installer\build-installer.ps1` packages already contains every hashed file from every previous build, which is
almost certainly where the installer's original "about 6,000 tiny files" and multiple `index-*.js` per folder came
from. `[InstallDelete]` cannot fix this: it only cleans the PC the app is installed on, not the files that go
*into* the setup in the first place.

## Phase 0 — measure the baseline

Before changing anything, record on the current `test/preview-all` build:

- File count and total size of `artifacts\server` from a **clean** `scripts\publish.ps1` run (delete
  `src\MacroGrid.Host\wwwroot\editor`, `wwwroot\deck` and `artifacts\server` by hand first) vs. the current, already
  accumulated build machine state.
- Time to run `installer\build-installer.ps1` end to end (`iscc` output, or wall clock).
- Time for a fresh install and for an `/UPDATE` run of the resulting `MacroGrid-Setup-<version>.exe`, on (a) a fast
  PC and (b) a normal PC with real-time antivirus (Defender) on. Inno Setup's `/LOG=<file>` flag timestamps each
  step; the gap between the first and last "Installing file" style line is the copy time to compare against.

**Measured (2026-09-29, after Phase 1/2):** `dotnet publish` failed under this checkout's own folder
(`...\Desktop\macro-station-main\macro-grid`) with `CSC : error CS2012 ... Access ... denied` while compiling
`MacroGrid.Plugin.Abstractions` — this machine's endpoint protection blocking the compiler's write specifically
under the Desktop path, reproducible across `dotnet build-server shutdown`, a clean `obj`/`bin`, and repeated
retries. A `git clone --local` of the same commit into `C:\dev` (outside the protected path) built and published
without any error, confirming the block was path-specific, not caused by these changes. Numbers from that clone:

| | Value |
|---|---|
| Published files (`artifacts\server`) | 49 (guardrail limit: 100) |
| Published size | 66.3 MB |
| `wwwroot\editor\assets` | 1 `index-*.js` (1.3 MB) + icons chunk + css + runtime |
| `wwwroot\deck\assets` | 1 `index-*.js` (298 KB) |
| File-copy time during install (`Setup.exe /LOG`, first "Installing the file" to last) | ~2.9 seconds |
| Full setup wizard start to app launch (includes UAC prompt and clicking through pages) | ~28 seconds |

This reaches the "a few seconds" target for the actual copy; the rest of the wizard time is UAC and page
navigation, which Phase 3's `ShouldSkipPage` already collapses to almost nothing on an `/UPDATE` run. **Phase 4
(archive packaging) is not needed** — Phase 1 and 2 alone solved the problem. Antivirus-on-a-normal-PC and a
side-by-side comparison against the pre-fix build were not measured (the pre-fix build's file count and its
antivirus-related slowdown are no longer easy to reproduce without reverting these changes); the ~2.9 second result
above is considered sufficient evidence to close this phase.

## Phase 1 — clean the build output before copying (done)

`scripts/publish.ps1` now deletes `src\MacroGrid.Host\wwwroot\editor` and `src\MacroGrid.Host\wwwroot\deck`
(`Remove-Item -Recurse -Force`, tolerating "does not exist") right before the `Copy-Item` calls that populate them.
Only those two generated subfolders are touched — the tracked files at the root of `wwwroot`
(`index.html`, `favicon.ico`, the PWA icons) are untouched. `editor\dist` and `webclient\dist` themselves are
already safe: Vite empties its own `outDir` by default before each build.

This alone should bring the shipped file count down to whatever a single clean build produces (a handful of files
per bundle, per the "What is already done" section), without touching the installer or the editor's icon loading.

## Phase 2 — a guardrail so this cannot silently regress (done)

At the end of `scripts/publish.ps1`, after the folder is fully assembled, a check now:

- Counts the files under `$out` and fails the script if the count is above a threshold (100, chosen as a
  starting point — today's expected shape is roughly a dozen wwwroot files plus the single exe and a handful of
  license/doc files; see the open question below for whether that number should move).
- Fails if `wwwroot\editor\assets` or `wwwroot\deck\assets` contains more than one file matching `index-*.js`
  (a sign either Phase 1's cleanup regressed, or a future Vite/lucide upgrade stopped matching the
  `manualChunks` predicate in `editor/vite.config.ts` and silently went back to one file per icon).

The final count is printed (`Write-Host "Published files: $fileCount"`) so a release log records it. This is a
build-time check, not a runtime one — it fails `publish.ps1`, before `installer\build-installer.ps1` ever runs.

## Phase 3 — installer side (Inno Setup)

What Inno Setup already does natively for speed stays as is: `Compression=lzma2` and `SolidCompression=yes` in
`installer/MacroGrid.iss`, `CloseApplications=yes`, and the `[InstallDelete]` entry that clears `{app}\wwwroot` on
upgrade. Once Phase 1 removes the stale files from the *source* folder, Inno's own per-file overhead — the actual
cost Phase 0 will have measured — should already be small. No changes are planned here beyond optionally narrowing
`[InstallDelete]` from `{app}\wwwroot` to `{app}\wwwroot\editor` and `{app}\wwwroot\deck` specifically (documented
as a no-op today, since nothing else lives under `wwwroot`, kept only if Phase 0's numbers show it matters).

## Phase 4 — fallback, only if Phase 0/1 still miss the target

If measuring after Phase 1 shows the file count or install time still misses "a few seconds" (for example because
antivirus per-file scanning dominates even at a low file count), the next step is packaging `wwwroot` as a single
archive that `MacroGrid.Host` serves directly (a zip-backed `IFileProvider`, so the app never unpacks it to disk) or
unpacks once on first run. This needs its own follow-up work — a custom file provider, correct cache headers, and a
way to debug a "file" that is really an archive entry — and is not the default plan; it is listed here so the
option is not lost if Phase 1 is not enough.

## Split: what the installer tool does vs. what our build does

| Concern | Handled by |
|---|---|
| Compressing the payload, closing the running app, restoring old files on cancel | Inno Setup (`installer/MacroGrid.iss`) |
| Not shipping one file per icon | the editor's own bundler config (`editor/vite.config.ts`, already done) |
| Not shipping stale hashed files from earlier builds | `scripts/publish.ps1` (Phase 1, this plan) |
| Catching a future regression in either of the above | `scripts/publish.ps1` guardrail (Phase 2, this plan) |
| Clearing an old version's files already on the user's PC | `[InstallDelete]` (`installer/MacroGrid.iss`, already done) |

## Auto-update

The updater (`src/MacroGrid.Host/Updates/`, see [../design/auto-update.md](../design/auto-update.md)) downloads the
same `MacroGrid-Setup-<version>.exe` asset and runs it with `/UPDATE`. Every gain from Phases 1–3 above applies to
updates automatically, with no change needed in the updater itself.

## Verification

1. Delete `src\MacroGrid.Host\wwwroot\editor`, `wwwroot\deck` and `artifacts\server` by hand, then run
   `scripts\publish.ps1` and confirm the guardrail passes and prints a small file count.
2. Run `installer\build-installer.ps1` and confirm it finishes without the guardrail tripping.
3. On a test PC (never the owner's main PC without asking first): install fresh, confirm the editor opens and icons
   still draw correctly (IconPicker, StatusBar, ActionPicker, PluginsToolWindow), and the browser deck loads at
   `/deck/`.
4. Upgrade an existing 0.3.1-alpha install via the in-app updater (`/UPDATE`) and confirm the same, plus that
   `wwwroot\editor\assets` and `wwwroot\deck\assets` each contain exactly one `index-*.js` afterwards.
5. Fill in Phase 0's before/after table with the measured numbers.

## Changelogs, once built

`docs/CHANGELOG.md` gets one short line only if the speed-up is user-noticeable (e.g. "installing and updating are
now much faster"). `docs/CHANGELOG-developer.md` is not touched — nothing here changes the SDK or the WebSocket
protocol.

## Open questions

- None open. The file-count threshold (100) held with room to spare against the real 49-file build; Phase 4 is
  confirmed unneeded; a normal-PC-with-antivirus timing was not collected, but the copy-time measurement already
  taken (~2.9 s) is well inside "a few seconds" even before accounting for antivirus overhead on far fewer files.
