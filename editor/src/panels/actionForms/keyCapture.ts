/**
 * Maps a physical KeyboardEvent.code to the normalized key names the server's HotkeyParser/KnownKeys
 * accept (see server/src/MacroGrid.Core/Input/KnownKeys.cs). Using `code` (physical key) rather than
 * `key` (layout-dependent character) means the captured shortcut is the same regardless of keyboard layout.
 */
const CODE_TO_NAME: Record<string, string> = {
  Enter: "enter", NumpadEnter: "enter", Escape: "escape", Tab: "tab", Space: "space",
  Backspace: "backspace", Delete: "delete", Insert: "insert",
  Home: "home", End: "end", PageUp: "pageup", PageDown: "pagedown",
  ArrowUp: "up", ArrowDown: "down", ArrowLeft: "left", ArrowRight: "right",
  PrintScreen: "printscreen", Pause: "pause", CapsLock: "capslock",
  NumLock: "numlock", ScrollLock: "scrolllock", ContextMenu: "menu",
  NumpadAdd: "numadd", NumpadSubtract: "numsubtract", NumpadMultiply: "nummultiply",
  NumpadDivide: "numdivide", NumpadDecimal: "numdecimal",
  Equal: "plus", Minus: "minus", Comma: "comma", Period: "period", Semicolon: "semicolon",
  Slash: "slash", Backslash: "backslash", Quote: "quote", Backquote: "backquote",
  BracketLeft: "bracketleft", BracketRight: "bracketright",
  AudioVolumeMute: "volumemute", AudioVolumeDown: "volumedown", AudioVolumeUp: "volumeup",
  MediaTrackNext: "medianext", MediaTrackPrevious: "mediaprev", MediaStop: "mediastop", MediaPlayPause: "mediaplaypause",
};

const MODIFIER_CODES: Record<string, "ctrl" | "shift" | "alt" | "win"> = {
  ControlLeft: "ctrl", ControlRight: "ctrl",
  ShiftLeft: "shift", ShiftRight: "shift",
  AltLeft: "alt", AltRight: "alt",
  MetaLeft: "win", MetaRight: "win",
};

/** null for a code we don't have a name for (e.g. an unusual browser-only key) — the caller should ignore the keypress. */
export function codeToKeyName(code: string): string | null {
  if (CODE_TO_NAME[code]) return CODE_TO_NAME[code];
  if (/^Key[A-Z]$/.test(code)) return code.slice(3).toLowerCase();
  if (/^Digit[0-9]$/.test(code)) return code.slice(5);
  if (/^Numpad[0-9]$/.test(code)) return `num${code.slice(6)}`;
  if (/^F([1-9]|1[0-9]|2[0-4])$/.test(code)) return code.toLowerCase();
  return null;
}

export function codeToModifier(code: string): "ctrl" | "shift" | "alt" | "win" | null {
  return MODIFIER_CODES[code] ?? null;
}

const MODIFIER_ORDER = ["ctrl", "shift", "alt", "win"] as const;

/** Renders held modifiers + an optional main key as "ctrl+shift+s", matching the server's HotkeyParser format. */
export function formatCombo(modifiers: Set<string>, key: string | null): string {
  const parts: string[] = MODIFIER_ORDER.filter((m) => modifiers.has(m));
  if (key) parts.push(key);
  return parts.join("+");
}
