/**
 * The editor's guard against a plugin widget that kills its web view. The preview runs plugin widget code like the phone does, so a widget that runs
 * out of memory ends the editor's renderer. The window (WebView2) then reloads the editor with `widgetCrash=1`; this module remembers which plugins had
 * live widgets when that happened (written before each worker starts, kept in the browser's storage) and keeps their previews off until the person
 * turns them back on. Plugins are never blamed for a reload that came for another reason.
 */

const RUNNING_KEY = "macro-grid.editor.widgetsRunning";
const OFF_KEY = "macro-grid.editor.widgetsOff";
const CRASH_PARAM = "widgetCrash";

const listeners = new Set<() => void>();

function readList(key: string): string[] {
  try {
    const value = JSON.parse(localStorage.getItem(key) ?? "[]");
    return Array.isArray(value) ? value.filter((v): v is string => typeof v === "string") : [];
  } catch {
    return [];
  }
}

function writeList(key: string, list: string[]): void {
  try {
    localStorage.setItem(key, JSON.stringify(list));
  } catch {
    // Storage can be unavailable; the guard then simply does not remember.
  }
}

/** Plugins whose previews are off after a crash. */
export function offPlugins(): ReadonlySet<string> {
  return new Set(readList(OFF_KEY));
}

/** Called before a worker starts and after one ends, with the plugins that have live widgets now. */
export function reportRunning(plugins: string[]): void {
  writeList(RUNNING_KEY, plugins);
}

/** At start: when the window reloaded the editor after its renderer died, the plugins that were running are switched off. Returns them. */
export function takeCrash(search: string = location.search, replaceUrl: (url: string) => void = (url) => history.replaceState(null, "", url)): string[] {
  const params = new URLSearchParams(search);
  if (params.get(CRASH_PARAM) !== "1") return [];
  params.delete(CRASH_PARAM);
  const rest = params.toString();
  replaceUrl(location.pathname + (rest ? `?${rest}` : "") + location.hash);
  const blamed = readList(RUNNING_KEY);
  writeList(RUNNING_KEY, []);
  if (blamed.length === 0) return [];
  writeList(OFF_KEY, [...new Set([...readList(OFF_KEY), ...blamed])]);
  return blamed;
}

export function turnOn(plugin: string): void {
  writeList(OFF_KEY, readList(OFF_KEY).filter((id) => id !== plugin));
  for (const listener of listeners) listener();
}

export function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => void listeners.delete(listener);
}
