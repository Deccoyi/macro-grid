# Automation rules

A rule runs a list of actions by itself, without a button press: when a value crosses a limit, at a time of day, or when a device connects. Open **Preferences → Automation** to add, edit or delete rules.

Rules do not press keys. A rule cannot send a shortcut or type text, because it would press them in whatever window is in front. Such a step is refused and shown in [Diagnostic Messages](/guide/troubleshooting#diagnostic-messages).

## Triggers

| Trigger | Starts when |
|---|---|
| **When a value turns true** | A condition on a variable becomes true, for example `system.cpu` greater than `90`. The rule starts when the condition goes from false to true. It does not start because the server started or the rule was saved while the condition was already true. |
| **At a time** | The local time is the time you set (`HH:mm`) on one of the days you pick, every day by default. A time that passed while the PC was asleep or Macro Grid was closed is not made up later. |
| **When a device connects** | A paired device connects that was not connected before. You can pick one device, or any paired device. |

A variable that has no value counts as false in a condition.

## Steps

The steps are the same as a button's actions, including If blocks. See [Actions and macros](/guide/actions).

Use **Run now** in the rule list to try a rule. It ignores the on/off switch, the pause and the cooldown, but not the limits below.

## Limits

- **Wait at least … seconds between runs** is the cooldown of a rule. A rule does not start again before it has passed.
- A rule has at most one run waiting or running. Starts that come in meanwhile are dropped.
- At most four rules run at the same time; the others wait.
- A rule that keeps starting too often has some starts dropped (`P161`). If it keeps going, Macro Grid switches it off (`P162`). Both show in [Diagnostic Messages](/guide/troubleshooting#diagnostic-messages).

## Switching off and backup

- Each rule has its own on/off switch. **Pause all rules** stops every rule until you resume them.
- The list shows the last result of each rule.
- Rules are part of a backup. A restore adds them **switched off**, so check them before you switch them on. See [Profiles and pages](/guide/profiles#back-up-and-restore).

## Not possible yet

Rules that press keys, rules that react to other events, and rules that depend on the window in front.
