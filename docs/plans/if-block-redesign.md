# If block redesign

**Status: first slice built (view only). The rest is optional.**

## Problem

A rule is stored as a flat list: `core.if`, a step, `core.endIf`, a step. In the editor it was hard to see that the last step sits **outside** the If, because the End row was a faint dashed line with the word "End". A broken rule followed from that. An If with no End (a hand-edited or damaged list) also showed nothing in the list, only a profile warning (`W233`).

## What already exists

The editor already draws an If block from the flat list (see [../design/logic-steps.md](../design/logic-steps.md), mockup in [../design/if-block-ui-mockup.html](../design/if-block-ui-mockup.html)): indent and a coloured rail per nesting level, a header row with the condition editor, an Otherwise divider, empty-body placeholders, add-here slots inside any lane, drag and drop, move and remove of a whole block, collapse, and a limit of 3 nested Ifs. `ActionList` is the one component behind both screens that have an action list (the widget event editor and the Automation page), so those two screens already share it.

## Design

- **Container.** An If is the header card (what it tests), a lane of steps joined by a rail, an optional Otherwise divider with its own lane, and an End cap. Nested Ifs repeat this one level deeper.
- **End cap.** It says "End of If", repeats what the If tested and states that the steps below run in every case. A step directly under the cap is then plainly outside the block.
- **Unclosed If.** The header shows a warning "This If is not closed" with one button that adds the End at the end of the list. That is exactly where the server already ends such a block, so the rule runs the same before and after the click.
- **Add, remove, move.** Unchanged: add at any slot, an If brings its End, removing an If keeps its steps, moving moves the whole block, a move that would break a block is refused.
- **Validation.** The editor view marks the two cases the user can act on: an unclosed If (button) and a stray Otherwise or End (remove button, as before). Too-deep nesting is refused with a message when added. The profile check keeps its `W233`.

## Compatibility

No change to the stored format: still `core.if`, `core.else`, `core.endIf` in a flat list, no new field, no migration, no protocol change. The server (`ActionFlow`) and its shared test table are untouched. Old profiles and rules open as they are.

## Reuse

The pieces are in `editor/src/panels/IfBlock.tsx` (header, Otherwise row, End cap, rail style) and the helpers in `actionFlow.ts`. Any list that shows steps with If blocks uses `ActionList` and gets all of this. No other screen contains an If today. Dynamic rules and the dynamize dialog use only the condition editor, not blocks, so they need nothing here.

## Not done (needs a decision)

- Nested-tree rendering (a real box around the block) instead of a rail. It would change how drag, the insertion slots, collapse and "go to step" from the Error List find their rows, for a gain the End cap mostly gives already.
- A free-text label on an If (needs a new stored field).
