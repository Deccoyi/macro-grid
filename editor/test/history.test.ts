import { describe, expect, it } from "vitest";
import {
  createHistory, isDirty, markSaved, recordChange, redo, redoLabel, undo, undoLabel,
} from "../src/state/history";

describe("history (docs/design/editor-edit-commands.md, \"Undo / redo\")", () => {
  it("starts empty and clean", () => {
    const h = createHistory<string>();
    expect(h.past).toHaveLength(0);
    expect(h.future).toHaveLength(0);
    expect(isDirty(h)).toBe(false);
    expect(undoLabel(h)).toBeNull();
    expect(redoLabel(h)).toBeNull();
  });

  it("pushes a step and becomes dirty", () => {
    let h = createHistory<string>();
    h = recordChange(h, "a", "undo.moveWidget", { now: 0 });
    expect(h.past).toHaveLength(1);
    expect(isDirty(h)).toBe(true);
    expect(undoLabel(h)).toBe("undo.moveWidget");
  });

  it("undo restores the previous state and moves it to future", () => {
    let h = createHistory<string>();
    h = recordChange(h, "a", "undo.moveWidget", { now: 0 });
    const result = undo(h, "b");
    expect(result).not.toBeNull();
    expect(result!.state).toBe("a");
    expect(result!.history.past).toHaveLength(0);
    expect(result!.history.future).toHaveLength(1);
  });

  it("redo re-applies what undo reverted", () => {
    let h = createHistory<string>();
    h = recordChange(h, "a", "undo.moveWidget", { now: 0 });
    const undone = undo(h, "b")!;
    const redone = redo(undone.history, undone.state)!;
    expect(redone.state).toBe("b");
    expect(redone.history.future).toHaveLength(0);
    expect(redone.history.past).toHaveLength(1);
  });

  it("undo with nothing to undo returns null", () => {
    const h = createHistory<string>();
    expect(undo(h, "a")).toBeNull();
  });

  it("redo with nothing to redo returns null", () => {
    const h = createHistory<string>();
    expect(redo(h, "a")).toBeNull();
  });

  it("a new change clears future (redo is no longer possible)", () => {
    let h = createHistory<string>();
    h = recordChange(h, "a", "undo.moveWidget", { now: 0 });
    h = undo(h, "b")!.history;
    expect(h.future).toHaveLength(1);
    h = recordChange(h, "a", "undo.editProperty", { now: 1000 });
    expect(h.future).toHaveLength(0);
  });

  it("coalesces two changes with the same key inside the window into one step", () => {
    let h = createHistory<string>();
    h = recordChange(h, "a", "undo.editProperty", { coalesceKey: "widget:rect:1", now: 0 });
    h = recordChange(h, "b", "undo.moveWidget", { coalesceKey: "widget:rect:1", now: 100 });
    expect(h.past).toHaveLength(1);
    expect(h.past[0]!.state).toBe("a"); // still the state from before the FIRST change in the burst
  });

  it("does not coalesce once the window has passed", () => {
    let h = createHistory<string>();
    h = recordChange(h, "a", "undo.editProperty", { coalesceKey: "widget:rect:1", now: 0 });
    h = recordChange(h, "b", "undo.moveWidget", { coalesceKey: "widget:rect:1", now: 700 });
    expect(h.past).toHaveLength(2);
  });

  it("does not coalesce across a save", () => {
    let h = createHistory<string>();
    h = recordChange(h, "a", "undo.editProperty", { coalesceKey: "widget:rect:1", now: 0 });
    h = markSaved(h);
    h = recordChange(h, "b", "undo.moveWidget", { coalesceKey: "widget:rect:1", now: 100 });
    expect(h.past).toHaveLength(2);
  });

  it("markSaved clears dirty at the saved id, and undoing back to it clears dirty again", () => {
    let h = createHistory<string>();
    h = recordChange(h, "a", "undo.moveWidget", { now: 0 });
    h = markSaved(h);
    expect(isDirty(h)).toBe(false);
    const undone = undo(h, "b")!;
    h = undone.history;
    expect(isDirty(h)).toBe(true); // moved away from the saved state
  });

  it("caps past at HISTORY_LIMIT", () => {
    let h = createHistory<number>();
    for (let i = 0; i < 150; i++) h = recordChange(h, i, "undo.editProperty", { now: i * 1000 });
    expect(h.past.length).toBeLessThanOrEqual(100);
  });
});
