import { Copy, CopyPlus, Redo2, Scissors, Trash2, ClipboardPaste, Undo2 } from "lucide-react";
import { useT } from "../i18n/I18nContext";
import { useCommands } from "./CommandsContext";
import type { Command } from "./types";

/** One click-without-a-menu icon per Edit command, in the header toolbar next to the Save button — added
 * on top of docs/design/editor-edit-commands.md at the owner's request (docs/ui/editor-icons.md). Reads
 * straight from the command registry so it always matches the Edit menu and the shortcuts exactly. */
export function EditToolbar() {
  const { t } = useT();
  const { get } = useCommands();
  const ids = ["edit.undo", "edit.redo", "edit.cut", "edit.copy", "edit.paste", "edit.duplicate", "edit.delete"] as const;
  const icons: Record<(typeof ids)[number], React.ReactNode> = {
    "edit.undo": <Undo2 size={14} />,
    "edit.redo": <Redo2 size={14} />,
    "edit.cut": <Scissors size={14} />,
    "edit.copy": <Copy size={14} />,
    "edit.paste": <ClipboardPaste size={14} />,
    "edit.duplicate": <CopyPlus size={14} />,
    "edit.delete": <Trash2 size={14} />,
  };
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 2 }}>
      {ids.map((id) => <ToolbarButton key={id} command={get(id)} icon={icons[id]} fallbackLabel={id} t={t} />)}
    </div>
  );
}

function ToolbarButton({
  command, icon, fallbackLabel, t,
}: {
  command: Command | undefined;
  icon: React.ReactNode;
  fallbackLabel: string;
  t: ReturnType<typeof useT>["t"];
}) {
  const label = command ? (command.label ? command.label() : t(command.labelKey)) : fallbackLabel;
  const shortcut = command?.shortcuts?.[0];
  const disabled = !command || !command.enabled();
  return (
    <button
      type="button"
      title={shortcut ? `${label} (${shortcut})` : label}
      aria-label={label}
      disabled={disabled}
      onClick={() => command?.run()}
      style={{
        width: 26, height: 26, display: "flex", alignItems: "center", justifyContent: "center",
        border: "none", background: "transparent", borderRadius: 4,
        color: disabled ? "var(--ms-text-disabled)" : "var(--ms-text-secondary)",
        cursor: disabled ? "default" : "pointer",
      }}
      onMouseEnter={(e) => { if (!disabled) e.currentTarget.style.background = "var(--ms-bg-surface-raised)"; }}
      onMouseLeave={(e) => { e.currentTarget.style.background = "transparent"; }}
    >
      {icon}
    </button>
  );
}
