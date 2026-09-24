import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import { useT } from "../../i18n/I18nContext";
import { codeToKeyName, codeToModifier, formatCombo } from "./keyCapture";

interface HotkeyCaptureProps {
  value: string;
  onChange: (combo: string) => void;
}

/**
 * Focus the field and press the actual keys — no typing "ctrl+shift+s" by hand. Uses the physical
 * key (KeyboardEvent.code), so the captured shortcut is the same on any keyboard layout.
 */
export function HotkeyCapture({ value, onChange }: HotkeyCaptureProps) {
  const { t } = useT();
  const [display, setDisplay] = useState(value);
  const [capturing, setCapturing] = useState(false);
  const heldModifiers = useRef<Set<string>>(new Set());
  const pressedMainKey = useRef(false);

  useEffect(() => {
    if (!capturing) setDisplay(value);
  }, [value, capturing]);

  const onFocus = () => {
    heldModifiers.current = new Set();
    pressedMainKey.current = false;
    setCapturing(true);
    setDisplay(t("hotkey.pressKey"));
  };

  const onBlur = () => {
    setCapturing(false);
    setDisplay(value);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    e.preventDefault();

    const modifier = codeToModifier(e.code);
    if (modifier) {
      heldModifiers.current.add(modifier);
      setDisplay(`${formatCombo(heldModifiers.current, null)}+`);
      return;
    }

    const key = codeToKeyName(e.code);
    if (!key) return; // an unmapped key; ignore rather than commit garbage

    pressedMainKey.current = true;
    const combo = formatCombo(heldModifiers.current, key);
    setDisplay(combo);
    onChange(combo);
  };

  const onKeyUp = (e: KeyboardEvent<HTMLInputElement>) => {
    const modifier = codeToModifier(e.code);
    if (!modifier) return;

    // A single modifier pressed and released alone (no other modifier, no main key) is itself a valid shortcut (e.g. "win").
    const wasTheOnlyOne = heldModifiers.current.size === 1 && heldModifiers.current.has(modifier);
    heldModifiers.current.delete(modifier);
    if (wasTheOnlyOne && !pressedMainKey.current) {
      onChange(modifier);
      setDisplay(modifier);
    }
  };

  return (
    <div style={{ display: "flex", gap: 6 }}>
      <input
        type="text"
        readOnly
        value={display}
        onFocus={onFocus}
        onBlur={onBlur}
        onKeyDown={onKeyDown}
        onKeyUp={onKeyUp}
        placeholder={t("hotkey.placeholder")}
        style={{ cursor: "text", background: capturing ? "var(--ms-accent-bg-muted)" : undefined }}
      />
      {value && (
        <button type="button" className="ghost" onClick={() => { onChange(""); setDisplay(""); }}>
          {t("hotkey.clear")}
        </button>
      )}
    </div>
  );
}
