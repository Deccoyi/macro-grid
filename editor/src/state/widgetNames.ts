import type { Widget } from "@macro/renderer";

/** The editor's mirror of the server's WidgetNames: the same rules, and both read tests/shared/widget-name-cases.json. */
export const MAX_NAME_LENGTH = 64;

const PREFIXES: Record<string, string> = {
  button: "Button", toggle: "Toggle", label: "Label", slider: "Slider", knob: "Knob", image: "Image", web: "Web",
};

/** Control characters removed, trimmed, cut to 64 characters; undefined when nothing is left. */
export function normalizeWidgetName(name: string | undefined | null): string | undefined {
  if (name == null) return undefined;
  // eslint-disable-next-line no-control-regex
  let cleaned = name.replace(/[\u0000-\u001f\u007f-\u009f]/g, "").trim();
  if (cleaned.length > MAX_NAME_LENGTH) cleaned = cleaned.slice(0, MAX_NAME_LENGTH).trimEnd();
  return cleaned.length === 0 ? undefined : cleaned;
}

function lowerSet(taken: Iterable<string | undefined | null>): Set<string> {
  const used = new Set<string>();
  for (const n of taken) if (n) used.add(n.toLowerCase());
  return used;
}

/** `Prefix_n` with the smallest n of 1 or more that is not taken. */
export function defaultWidgetName(type: string, taken: Iterable<string | undefined | null>): string {
  const used = lowerSet(taken);
  const prefix = PREFIXES[type] ?? "Widget";
  for (let n = 1; ; n++) {
    const candidate = `${prefix}_${n}`;
    if (!used.has(candidate.toLowerCase())) return candidate;
  }
}

/** The wanted name made unique: itself when free, else with _2, _3, ... appended (the base is cut so the result fits). */
export function uniqueWidgetName(name: string, taken: Iterable<string | undefined | null>): string {
  const used = lowerSet(taken);
  if (!used.has(name.toLowerCase())) return name;
  for (let n = 2; ; n++) {
    const suffix = `_${n}`;
    const base = name.length + suffix.length > MAX_NAME_LENGTH ? name.slice(0, MAX_NAME_LENGTH - suffix.length) : name;
    const candidate = base + suffix;
    if (!used.has(candidate.toLowerCase())) return candidate;
  }
}

/** Gives every widget of a page a valid, page-unique name (existing valid names are kept; the first of two equal names keeps it). Edits in place. */
export function ensureWidgetNames(widgets: Widget[]): void {
  const taken = new Set<string>();
  const repeats: { widget: Widget; wanted: string }[] = [];
  for (const widget of widgets) {
    const wanted = normalizeWidgetName(widget.name);
    if (wanted === undefined) continue;
    if (!taken.has(wanted.toLowerCase())) {
      taken.add(wanted.toLowerCase());
      widget.name = wanted;
    } else repeats.push({ widget, wanted });
  }
  for (const { widget, wanted } of repeats) {
    const name = uniqueWidgetName(wanted, taken);
    taken.add(name.toLowerCase());
    widget.name = name;
  }
  for (const widget of widgets) {
    if (normalizeWidgetName(widget.name) !== undefined) continue;
    const name = defaultWidgetName(widget.type, taken);
    taken.add(name.toLowerCase());
    widget.name = name;
  }
}

/** Names for widgets that are about to join a page (a duplicate, a paste, a move or copy): each keeps its name when it is free there. */
export function nameForPage(incoming: Widget[], pageWidgets: Widget[]): void {
  const taken = new Set<string>();
  for (const w of pageWidgets) if (w.name) taken.add(w.name.toLowerCase());
  for (const widget of incoming) {
    const wanted = normalizeWidgetName(widget.name);
    const name = wanted ? uniqueWidgetName(wanted, taken) : defaultWidgetName(widget.type, taken);
    taken.add(name.toLowerCase());
    widget.name = name;
  }
}
