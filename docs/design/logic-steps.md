# Logic steps in action lists

**Status: implemented.**

An action list can now decide: run some steps only when a condition is met, run others when it is not, or end early. Before this, a list ran every step, one after the other.

## Steps

The list stays **flat** (the profile format does not change). Four built-in action types steer it, all in the category Logic:

| Type | Name in the editor | Meaning |
|---|---|---|
| `core.if` | If | Starts a block. Its steps run only when the test is met. |
| `core.else` | Otherwise | Splits a block. Its steps run only when the If was not met. At most one per block. |
| `core.endIf` | End | Ends the block. |
| `core.stop` | Stop | Ends the whole list. Not a failure. |

`core.delay` ("Wait") keeps its id and its `ms` setting and moves to the same category. The editor shows it in seconds.

An If has a `when` setting:

- `condition` (default): the setting `condition` is a `ConditionNode`, the same JSON a dynamic rule uses (compare, not, and, or, xor, with the operators of [variable-types-and-conditions.md](variable-types-and-conditions.md)). A missing or unreadable condition counts as not met.
- `previousFailed` / `previousOk`: how the last step that actually ran ended. Steps that were skipped do not count, and before any step ran nothing has failed.

The condition is read when the If is reached, so an earlier step of the same list can change the answer.

## Server

`ActionFlow` (pure, in `MacroGrid.Core.Actions`) keeps two counters: how many Ifs are open and whether the current branch is skipped. `ActionDispatcher.DispatchAsync` asks it about every step before it looks for a handler: run it, skip it, or stop. A skipped step does not call its handler, so a skipped "while held" step is not started; its release is still paired as before.

A list that does not match still runs safely: a missing End means the block goes to the end of the list, a stray Otherwise or End does nothing. There are no loops, so a run always ends. The four types are also registered as handlers that do nothing, so the catalog knows their names and an older list does not report them as missing.

`ActionFlow.Depths` gives the indentation of each row; the editor has the same rule in its own pure module and a shared table of cases (`tests/shared/action-flow-cases.json`) keeps the two readings of a list in step.

## Editor

- Rows are indented by depth (at most 3 nested Ifs). If, Otherwise and End are slim rows without a type picker; an End is never removed alone.
- The If row holds the `when` choice and, for a condition, the same condition editor as the dynamic rules (without the "This button" variables, which have no value in an action).
- "Add otherwise" adds the Otherwise before the End. Collapsing an If shows one line: what it tests and how many steps it holds. This is view state only. A row opened from the Error List opens the blocks that hide it.
- Moving an If moves its whole block. A move that would break a block is refused. Removing an If removes its Otherwise and End and keeps the steps inside.
- The picker offers If, Wait and Stop under Logic. Picking If adds an If and its End together.
- The profile check warns about lists that do not match or nest too deep (`W233`), an If with no condition (`W234`), and variables in the condition that nobody provides (`W230`, `W231`, `W232`).

## Known limits

- A failure that an If reacts to is still reported (toast, status bar, last result).
- A Wait holds the device's action queue and cannot be cancelled.
- A "while held" step that is skipped by an If still gets its release call when the button is released.
