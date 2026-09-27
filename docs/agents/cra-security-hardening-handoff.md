# Handoff: CRA-driven security hardening (branch `security/pairing-and-logging`)

Written 2026-09-27 for whichever agent picks this up next. The previous agent session (this one) is stepping away; nothing
here is decided beyond what is explicitly marked done. Read this before touching the branch, the plan or the report.

## What this is

The owner asked for a CRA (EU Cyber Resilience Act) compliance review of the three Macro Grid repositories, then said "let's
comply as much as we reasonably can" and had this agent implement the resulting security fixes. It is **not** a CRA legal
requirement (the project is very likely out of scope — free, not monetised, no legal entity), just good practice the review
surfaced.

## Where everything lives

| What | Where |
|---|---|
| The CRA review report (Turkish) | `C:\Users\coskun.yildiz\Desktop\macro-station-main\cra-rapor.md` — **not in any repo**, lives loose on the owner's Desktop |
| The implementation plan/log (Turkish) | `C:\Users\coskun.yildiz\.claude\plans\cyber-resilliance-act-yasas-n-silly-sparkle.md` — **not in any repo**, a Claude Code plan-mode file on the owner's machine. Checkboxes track exactly what was done. |
| Risk assessment (built) | [`docs/design/security-risk-assessment.md`](../design/security-risk-assessment.md) (this repo) |
| Remaining hardening work (planned, not built) | [`docs/plans/security-hardening-plan.md`](../plans/security-hardening-plan.md) (this repo) — parts A (transport encryption), B (plugin secret storage), C (delete-my-data on uninstall) |
| This handoff note | right here |

The two Turkish files are the fullest record of *why* each decision was made and every owner answer; read the plan file first
if you need history, this file is the short version for picking up the branch itself.

## Branch state (all three repos, same branch name)

`security/pairing-and-logging`, based on each repo's `dev` **as of 2026-09-26** — since then `dev` has moved forward in at
least `macro-grid` (version unification to 1.0.x, plan file reorganisation) and `macro-grid-plugin` (Sound plugin renamed to
SoundBoard). The branch was already merged with `origin/dev` once (2026-09-26) to pick up that drift; it may need it again
before a PR is opened. **Check `git log dev..security/pairing-and-logging` and `git log security/pairing-and-logging..dev` in
each repo before doing anything else.**

Status per repo, all pushed to `origin`, all green on CI, **no PR opened yet in any of the three**:

- `macro-grid`: pairing mode + rate limiting, security log + log retention, plugin permission wording, CI vulnerability
  check + SBOM, DPAPI-encrypted device tokens, docs (SECURITY.md, architecture.md, changelogs, website guides, README), the
  risk assessment and the hardening plan.
- `macro-grid-client`: CI vulnerability check + SBOM, docs (SECURITY.md, changelogs).
- `macro-grid-plugin`: Dependabot fix (Sound → SoundBoard path), CI vulnerability check + SBOM in the plugin release
  script, plugin network-access wording in SECURITY.md, docs.

## What was decided (don't relitigate these)

- **Pairing mode, not a rotating timed PIN.** The owner explicitly chose this over a PIN that changes every N seconds. A PIN
  is valid only while the editor's Pairing window is open (polled, 15 s lease), for at most 5 minutes, single-use. See
  `PairingService` in `macro-grid/src/MacroGrid.Core/Devices/`.
- **No promises in SECURITY.md.** The owner said explicitly: don't promise a support period or that fixes will happen —
  frame everything as "this is a hobby project, no guarantees." This is recorded as a standing preference in the owner's
  cross-session memory (`feedback_no_support_promises.md`) — read it before writing any more security/support docs.
- **History rewrite: no.** A git-history audit found the owner's real name and personal Gmail in web-UI PR-merge commits on
  `main`/`dev` of all three repos (both addresses are the owner's own, not a leak). The owner chose to leave history as is
  rather than rewrite it. Don't raise this again unless the owner does.
- **Repo hardening (branch protection additions) not applied.** The owner was offered: protect `dev` from force-push/delete,
  restrict CI to an allow-list of actions + require SHA pinning, require an approver on the `nuget` publish environment.
  Owner gave no preference yet — these are still open, not declined.

## What is verified vs. not

- All server unit tests pass (434 as of the last commit on this branch), CI is green in all three repos (build, typecheck,
  vulnerability check, dependency-review skip-on-non-PR).
- **Nothing was run against a real device.** Pairing-window behavior (lease expiry, wrong-PIN blocking, PIN replacement) and
  the DPAPI token migration (an existing `devices.json` converting on first load) have only been exercised by unit tests with
  a fake clock/protector — never against a live server + phone. The owner and the previous agent agreed this should happen
  before merging, but it hadn't happened yet when this handoff was written.

## Suggested next steps, in the order discussed with the owner

1. Manual test with a real phone: pairing-window lease/lifecycle, wrong-PIN blocking, and that an existing paired device's
   token survives the DPAPI migration without re-pairing.
2. Open PRs (`security/pairing-and-logging` → `dev`) in all three repos — after re-merging `dev` if it has drifted further —
   and let the owner review/merge. **Do not open PRs without the owner's go-ahead in that session**; this has been a
   recurring point of friction (see `feedback_ask_before_commit.md` in owner memory) — every commit, push and especially PR
   is a separate ask, not a standing permission.
2b. If PRs are wanted, coordinate with whichever session is touching `docs/plans/README.md` on `dev` right now (as of this
    writing that file already has a note: *"Not listed here: `security-hardening-plan.md`, on the unmerged
    `security/pairing-and-logging` branch... It will get a row once that branch is on `dev`."* — another agent added that,
    expecting this branch to land eventually).
3. A version bump (owner suggested 1.1.0-ish, MINOR — the pairing protocol's error *behavior* changed slightly and the
   device-token file format changed) once merged.
4. Then the plan's remaining parts, smallest first: B (plugin secret storage via a new optional SDK API — needs an SDK
   release), then C (delete-my-data on uninstall — needs testing on a clean PC with Inno Setup, which was not available in
   this environment), then A (transport encryption — the big one, needs a native WebSocket layer in the Android app).

## Things to know about the working environment

- No local Inno Setup compiler was available to test `installer/MacroGrid.iss` changes; part C of the hardening plan needs
  that.
- This repo's working directory has been shared between at least two concurrent agent sessions (this one and a
  general-coordination session referring to itself as "Master Reis"). If you find the checked-out branch is not what you
  expect, or `dev` has moved since the summary above, that is why — re-check before assuming this note is stale.
- `security-hardening-plan.md` deliberately avoids third-party product names per this repo's `CLAUDE.md` naming rule (it
  says "OBS" once for the plugin's own settings field, which is the functional integration target itself — that's the
  allowed exception, not an oversight).

Delete this file once the branch is merged and the plan's open items have owners/dates, per this folder's own rule
(`docs/agents/README.md`): a note is deleted when nothing about it needs a decision anymore.
