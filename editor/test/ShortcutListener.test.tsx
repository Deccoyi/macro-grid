import { render, fireEvent } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { CommandsProvider, useRegisterCommands } from "../src/commands/CommandsContext";
import { ShortcutListener } from "../src/commands/ShortcutListener";
import type { Command } from "../src/commands/types";

function Harness({ run }: { run: () => void }) {
  const command: Command = {
    id: "edit.delete",
    labelKey: "edit.delete",
    shortcuts: ["Delete"],
    enabled: () => true,
    run,
  };
  useRegisterCommands([command]);
  return (
    <div>
      <input data-testid="field" />
      <div data-testid="canvas" tabIndex={0} />
    </div>
  );
}

/** docs/design/editor-edit-commands.md, "Command registry": shortcuts must not fire while a text field
 * has focus — the field's own native undo/copy/paste keeps working — except Escape. */
describe("ShortcutListener text-field guard", () => {
  it("does not run the command while an input has focus", () => {
    const run = vi.fn();
    const { getByTestId } = render(
      <CommandsProvider>
        <ShortcutListener />
        <Harness run={run} />
      </CommandsProvider>,
    );
    const field = getByTestId("field");
    field.focus();
    fireEvent.keyDown(field, { key: "Delete" });
    expect(run).not.toHaveBeenCalled();
  });

  it("runs the command once focus leaves the text field", () => {
    const run = vi.fn();
    const { getByTestId } = render(
      <CommandsProvider>
        <ShortcutListener />
        <Harness run={run} />
      </CommandsProvider>,
    );
    const field = getByTestId("field");
    const canvas = getByTestId("canvas");
    field.focus();
    field.blur();
    canvas.focus();
    fireEvent.keyDown(canvas, { key: "Delete" });
    expect(run).toHaveBeenCalledTimes(1);
  });

  it("still lets Escape through while a text field has focus", () => {
    const run = vi.fn();
    function EscapeHarness() {
      const command: Command = { id: "edit.clearSelection", labelKey: "edit.selectAll", shortcuts: ["Escape"], enabled: () => true, run };
      useRegisterCommands([command]);
      return <input data-testid="field" />;
    }
    const { getByTestId } = render(
      <CommandsProvider>
        <ShortcutListener />
        <EscapeHarness />
      </CommandsProvider>,
    );
    const field = getByTestId("field");
    field.focus();
    fireEvent.keyDown(field, { key: "Escape" });
    expect(run).toHaveBeenCalledTimes(1);
  });
});
