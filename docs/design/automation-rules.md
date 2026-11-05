# Automation rules

A rule runs a list of actions by itself, without a button press. Rules live in Preferences > Automation and are stored in the data folder, not in a profile. There is no new action type and no SDK or protocol change: a rule's steps are the same bindings a button uses.

## The model

```
AutomationRule { Id, Name, Enabled, Trigger, Actions: List<ActionBinding>, CooldownSeconds }
AutomationTrigger { Kind: "variable" | "variableChange" | "time" | "deviceConnect",
                    Condition: ConditionNode?      // variable
                    Time: "HH:mm"?, Days: int[]?   // time; days 0-6 (Sunday = 0), empty = every day
                    DeviceId: string?              // deviceConnect; null = any paired device
                    Variable, Mode, From, To, Threshold, MinIntervalSeconds }   // variableChange (below)
```

`AutomationRule` and `AutomationTrigger` keep fields they do not know (`[JsonExtensionData]`), so a file written by a newer build survives a round trip through this one. The other way round does not work: a build without `variableChange` refuses to save a list that contains such a rule ("has no known trigger") until that rule is deleted. Rules live only in `automation.json` and in a backup, never in a profile package.

`AutomationStore` (`Core/Automation/`) keeps the list in `automation.json` (`formatVersion` 1, `paused`, `rules`). It is written through a `.tmp` file, a file that cannot be read is moved to `.broken`, and `TryReplace` is all or nothing. An action type that is not registered is accepted, because its plugin may be switched off; it fails when the rule runs, like on a button.

## When a rule starts

`AutomationService` is a hosted service with one loop every 250 ms. Event handlers only record what changed; every decision is made on the loop.

| Trigger | Starts | Does not start |
| --- | --- | --- |
| `variable` | when the condition goes from false to true | when the server starts or the rule is saved while the condition already holds, or while it stays true; a crossing shorter than one tick can be missed |
| `variableChange` | when the watched variable's value differs from the last value judged (see below) | when the server starts or the rule is saved, when the variable appears or disappears, in the 3 s after it appeared, for a value that is back where it was within one tick, for changes made while the rule runs |
| `time` | when the local minute equals `Time` on an allowed weekday, once per minute | for a minute that passed while the PC slept or Macro Grid was closed (nothing is caught up) |
| `deviceConnect` | when a paired device appears in the session list that was not there before | when a device that is already connected opens a second session |

The condition of a `variable` rule is an ordinary rules condition (see [variable-types-and-conditions.md](variable-types-and-conditions.md)); a variable that is not available makes it false.

### The `variableChange` trigger

One global variable by full name (`self.*` is refused when saving). `Mode`: `any` (optionally narrowed by `From` / `To`, compared as numbers when both sides are numbers, otherwise as text without case), `crosses` (a number goes from below `Threshold` to at or above it, or back) or `contains` (the new text contains `To`). `MinIntervalSeconds` (0-3600) is a start-to-start gap on top of the cooldown. The editor shows "it becomes" as mode `any` with `To`.

Detection is on the 250 ms loop, from the same variable-changed event as the `variable` trigger; there is no polling. The rule remembers the last value it judged. Rebuilding the rule reads the current value without firing. A missing value on either side never fires; when a value first appears (or comes back, for instance after a plugin reload) the rule only follows it for 3 s. Writes inside one tick merge, so a flip and flip back is no change, and the old value is the one at the previous judged tick. While the rule runs, changes of its variable are followed without starting (re-entry guard) and the value is read again when the run ends. Five such changes within 60 s report `P164`; the guard also hides a real outside change during a long run. Loops through other rules end in `P162` as before.

**Tokens.** The steps (text fields that allow variables, and the conditions of If steps) can read `{trigger.old}` and `{trigger.new}`: the values as text (booleans `true`/`false`, numbers with the invariant culture), control characters removed, at most 256 characters, because a plugin variable can carry text from the internet. A store that wraps the real one answers them and is passed to `ActionDispatcher.RunListAsync`; there is no SDK or protocol change. "Run now" has no values, so the tokens are empty there. The tokens are not in the variable picker.

## What a rule may do

A start goes through these checks, in this order. A start that fails one is dropped.

1. The list is not paused and the rule is on.
2. The same rule is not still running.
3. At least `max(1 s, CooldownSeconds)` since its last start.
4. The rule's own bucket (5 in a row, then one every 2 s) and one bucket for all rules (20 in a row, then 5 a second).

At most four rule lists run at once; the others wait. Each rule has at most one run waiting or running.

A rule runs with `ActionContext.UserGesture` false, so a JavaScript plugin action cannot type for it. It has no device, so `core.page`, `core.profile` and the web step fail with a clear message, unless the rule is a `deviceConnect` rule: then the device that connected is the context and those steps act on it.

The "Show page" step of a rule saves the profile with the page (`profileId` next to `pageId`): the device first switches to that profile, then shows the page, because a rule has no open profile to look the page up in. A step saved earlier with only a `pageId` works as before (the page is looked up in the device's current profile).

**Key-pressing steps are refused.** `core.hotkey` and `core.typeText` send keys to whatever window is in front when the rule fires, so a rule that contains one reports it (`P160`) instead of pressing. The set is `AutomationService.KeyboardSteps`; allowing them is a few lines there plus removing the editor note.

## Loop guard and the Error List

A rule that is refused by its own bucket 10 times within 60 s is switched off by the host and saved as off. This also stops loops that go through a plugin or another program, which a call counter would not see. The false-to-true rule already removes the simplest loops.

The Error List (source `automation`, one line per rule and kind) shows:

| Code | Meaning | Clears |
| --- | --- | --- |
| `P160` | a step failed (the first failure's text) | after a later successful run |
| `P161` | starts were dropped because the rule started too often | when the rule is deleted |
| `P162` | the rule was switched off because it kept starting | when the rule is deleted |
| `P163` | the rule uses a user variable that is not in the Global Variable List (checked when rules are saved and when a variable is deleted) | when the variable exists again or the rule changes |
| `P164` | a `variableChange` rule keeps changing the variable that starts it (its own changes are ignored) | when the rule is deleted |

The numbers (50 rules, 20 steps, 5 and 20 starts, 10 refusals in 60 s, 4 at once) are first guesses and live in `AutomationLimits` and the service constants. There is no separate write limiter for variables: rule starts are capped, each start has at most 20 steps, and unchanged writes are silent.

## The editor

- Preferences > Automation lists the rules with an on/off switch, the trigger in words, the last result and "Run now". A pause switch stops all rules.
- A rule that is not finished (no name, a time that is not `HH:mm`, an incomplete condition) stays on the page and is not sent to the server.
- The steps are edited with the same list a button uses (`panels/ActionList.tsx`), including If blocks.
- Deleting a global variable also lists the rules that use it (`GET /api/automation/usage?name=`).
- "Run now" ignores the on/off switch, the pause and the cooldown, but not the overlap rule or the buckets. A `deviceConnect` rule uses the first connected matching device.

## Backup

`automation.json` is its own entry in a `.mgbackup` ([backup-restore.md](backup-restore.md)); a build that does not know it skips it. A restore merges by id and never removes a rule, and every rule it adds or replaces is saved **switched off**, because a backup can come from someone else and a restore must not start actions by itself. The pause switch keeps its value.

## Not built

- Rules that press keys (see above).
- A rule that reacts to an event other than the three triggers, a plugin trigger, or a trigger that depends on the window in front.
- A hard cap per variable write.
- A live view of rule runs; the status is read when the page opens and after "Run now".
