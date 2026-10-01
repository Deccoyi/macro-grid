# Automation rules

A rule runs a list of actions by itself, without a button press. Rules live in Preferences > Automation and are stored in the data folder, not in a profile. There is no new action type and no SDK or protocol change: a rule's steps are the same bindings a button uses.

## The model

```
AutomationRule { Id, Name, Enabled, Trigger, Actions: List<ActionBinding>, CooldownSeconds }
AutomationTrigger { Kind: "variable" | "time" | "deviceConnect",
                    Condition: ConditionNode?      // variable
                    Time: "HH:mm"?, Days: int[]?   // time; days 0-6 (Sunday = 0), empty = every day
                    DeviceId: string? }            // deviceConnect; null = any paired device
```

`AutomationStore` (`Core/Automation/`) keeps the list in `automation.json` (`formatVersion` 1, `paused`, `rules`). It is written through a `.tmp` file, a file that cannot be read is moved to `.broken`, and `TryReplace` is all or nothing. An action type that is not registered is accepted, because its plugin may be switched off; it fails when the rule runs, like on a button.

## When a rule starts

`AutomationService` is a hosted service with one loop every 250 ms. Event handlers only record what changed; every decision is made on the loop.

| Trigger | Starts | Does not start |
| --- | --- | --- |
| `variable` | when the condition goes from false to true | when the server starts or the rule is saved while the condition already holds, or while it stays true; a crossing shorter than one tick can be missed |
| `time` | when the local minute equals `Time` on an allowed weekday, once per minute | for a minute that passed while the PC slept or Macro Grid was closed (nothing is caught up) |
| `deviceConnect` | when a paired device appears in the session list that was not there before | when a device that is already connected opens a second session |

The condition of a `variable` rule is an ordinary rules condition (see [variable-types-and-conditions.md](variable-types-and-conditions.md)); a variable that is not available makes it false.

## What a rule may do

A start goes through these checks, in this order. A start that fails one is dropped.

1. The list is not paused and the rule is on.
2. The same rule is not still running.
3. At least `max(1 s, CooldownSeconds)` since its last start.
4. The rule's own bucket (5 in a row, then one every 2 s) and one bucket for all rules (20 in a row, then 5 a second).

At most four rule lists run at once; the others wait. Each rule has at most one run waiting or running.

A rule runs with `ActionContext.UserGesture` false, so a JavaScript plugin action cannot type for it. It has no device, so `core.page`, `core.profile` and the web step fail with a clear message, unless the rule is a `deviceConnect` rule: then the device that connected is the context and those steps act on it.

**Key-pressing steps are refused.** `core.hotkey` and `core.typeText` send keys to whatever window is in front when the rule fires, so a rule that contains one reports it (`P160`) instead of pressing. The set is `AutomationService.KeyboardSteps`; allowing them is a few lines there plus removing the editor note.

## Loop guard and the Error List

A rule that is refused by its own bucket 10 times within 60 s is switched off by the host and saved as off. This also stops loops that go through a plugin or another program, which a call counter would not see. The false-to-true rule already removes the simplest loops.

The Error List (source `automation`, one line per rule and kind) shows:

| Code | Meaning | Clears |
| --- | --- | --- |
| `P160` | a step failed (the first failure's text) | after a later successful run |
| `P161` | starts were dropped because the rule started too often | when the rule is deleted |
| `P162` | the rule was switched off because it kept starting | when the rule is deleted |

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
