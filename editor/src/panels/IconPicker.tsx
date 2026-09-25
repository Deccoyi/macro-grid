import { useEffect, useMemo, useState } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import dynamicIconImports from "lucide-react/dynamicIconImports";
import { api } from "../api/client";
import type { IconPackInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { PickerShell, usePickerOpenState } from "./PickerShell";

const ALL_NAMES = Object.keys(dynamicIconImports).sort();
const LUCIDE_PACK_ID = "lucide";

// A starting point so the picker isn't empty before the user searches.
const POPULAR = [
  "mouse-pointer-click", "keyboard", "mic", "mic-off", "volume-2", "volume-x", "play", "pause",
  "square", "video", "camera", "monitor", "monitor-play", "settings", "power", "home", "star",
  "heart", "bell", "folder", "file", "image", "music", "headphones", "gamepad-2", "tv", "radio",
  "wifi", "bluetooth", "battery", "sun", "moon", "lock", "unlock", "trash-2", "copy", "clipboard",
  "scissors", "save", "download", "upload", "share-2", "link", "refresh-cw", "rotate-cw",
  "chevron-right", "chevron-left", "arrow-up", "arrow-down", "plus", "minus", "x", "check",
  "alert-triangle", "info", "layers", "grid-3x3", "twitch", "youtube", "message-circle",
];

const MAX_RESULTS = 96;

// Plugin-contributed packs (IPluginHost.RegisterIconPack) rarely change while the editor is open — one
// fetch per session is enough, and it lets `iconToDataUri` resolve a stored icon name without the caller
// having opened the picker first (e.g. recoloring an already-picked icon from TextFields.tsx).
let iconPacksPromise: Promise<IconPackInfo[]> | null = null;
/** Forgets the cached pack list so the next lookup refetches it (a plugin was installed, reloaded or removed). */
export function invalidateIconPacks() {
  iconPacksPromise = null;
}
function fetchIconPacks(): Promise<IconPackInfo[]> {
  iconPacksPromise ??= api.listIconPacks().catch(() => []);
  return iconPacksPromise;
}

const pluginSvgCache = new Map<string, string | null>();
async function fetchPluginSvg(packId: string, name: string): Promise<string | null> {
  const key = `${packId}/${name}`;
  if (pluginSvgCache.has(key)) return pluginSvgCache.get(key)!;
  const svg = await api.getIconPackIconSvg(packId, name).catch(() => null);
  pluginSvgCache.set(key, svg);
  return svg;
}

async function lucideToDataUri(name: string, color: string): Promise<string | null> {
  const load = dynamicIconImports[name as keyof typeof dynamicIconImports];
  if (!load) return null;
  const mod = await load();
  const Icon = mod.default;
  const svg = renderToStaticMarkup(<Icon color={color} size={24} strokeWidth={2} />);
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

/** A plugin-contributed pack's SVGs use stroke="currentColor" but are rendered as a standalone image
 * (no CSS cascade from the page), so `currentColor` would otherwise fall back to black. Setting `color`
 * on the root <svg> gives that document its own current color to inherit from. */
function coloredDataUri(svg: string, color: string): string {
  const withColor = svg.includes("<svg ") ? svg.replace("<svg ", `<svg color="${color}" `) : svg;
  return `data:image/svg+xml;utf8,${encodeURIComponent(withColor)}`;
}

async function pluginIconToDataUri(packId: string, name: string, color: string): Promise<string | null> {
  const svg = await fetchPluginSvg(packId, name);
  return svg ? coloredDataUri(svg, color) : null;
}

/** Resolves a stored icon name (from WidgetStyle.iconName) back into a colored data: URI, regardless of
 * which pack it came from. Lucide is checked first (sync, no network) since it's the common case. */
async function iconToDataUri(name: string, color: string): Promise<string | null> {
  if (dynamicIconImports[name as keyof typeof dynamicIconImports]) return lucideToDataUri(name, color);

  const packs = await fetchIconPacks();
  const pack = packs.find((p) => p.icons.includes(name));
  return pack ? pluginIconToDataUri(pack.id, name, color) : null;
}

interface IconPickerProps {
  /** The widget's current icon, as a data: URI (what WidgetStyle.icon holds). */
  value?: string;
  /** Baked into the generated SVG's stroke color at pick time — usually the widget's own foreground. */
  color?: string;
  onChange: (dataUri: string | undefined, iconName: string | undefined) => void;
}

/** "Pick icon…" button + a search modal over lucide-react (ISC licensed, bundled locally) plus any
 * plugin-contributed icon packs (fetched from the server, e.g. the PLC icon pack). */
/** Falls back to the editor's own current text color (not a hardcoded dark-theme hex) so icon previews
 * stay visible against `--ms-bg-inset` in both themes — a fixed "#e6e7ea" (light gray) used to render
 * invisible on the light theme's near-white inset background. */
function defaultIconColor(): string {
  if (typeof document === "undefined") return "#e6e7ea";
  return getComputedStyle(document.documentElement).getPropertyValue("--ms-text-primary").trim() || "#e6e7ea";
}

export function IconPicker({ value, color, onChange }: IconPickerProps) {
  const { t } = useT();
  const picker = usePickerOpenState();
  const effectiveColor = color ?? defaultIconColor();
  const [pluginPacks, setPluginPacks] = useState<IconPackInfo[]>([]);
  const activePack = picker.category;

  useEffect(() => {
    let cancelled = false;
    fetchIconPacks().then((packs) => { if (!cancelled) setPluginPacks(packs); });
    return () => { cancelled = true; };
  }, []);

  const categories = useMemo(
    () => [
      { id: LUCIDE_PACK_ID, label: "Lucide", badge: ALL_NAMES.length },
      ...pluginPacks.map((p) => ({ id: p.id, label: p.displayName, badge: p.icons.length })),
    ],
    [pluginPacks],
  );

  // Each entry carries its own pack id so "All" can mix lucide names with plugin-pack names that might
  // collide (e.g. two packs both having a "play" icon) without ambiguity.
  const entries = useMemo(() => {
    const q = picker.query.trim().toLowerCase();
    const lucideNames = q ? ALL_NAMES.filter((n) => n.includes(q)) : POPULAR;
    const lucideEntries = lucideNames.map((name) => ({ packId: LUCIDE_PACK_ID, name }));
    const pluginEntries = pluginPacks.flatMap((p) =>
      (q ? p.icons.filter((n) => n.includes(q)) : p.icons).map((name) => ({ packId: p.id, name })));

    if (activePack === "all") return [...pluginEntries, ...lucideEntries].slice(0, MAX_RESULTS);
    if (activePack === LUCIDE_PACK_ID) return lucideEntries.slice(0, MAX_RESULTS);
    return pluginEntries.filter((e) => e.packId === activePack).slice(0, MAX_RESULTS);
  }, [picker.query, activePack, pluginPacks]);

  return (
    <div>
      <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
        {value ? (
          <img src={value} width={22} height={22} alt="" style={{ background: "var(--ms-bg-inset)", borderRadius: 4, padding: 2 }} />
        ) : (
          <span style={{ color: "var(--ms-text-secondary)", fontSize: 12 }}>{t("icon.none")}</span>
        )}
        <button type="button" className="ghost" onClick={picker.openPicker}>{t("icon.pick")}</button>
        {value && <button type="button" className="ghost" onClick={() => onChange(undefined, undefined)}>{t("icon.remove")}</button>}
      </div>

      {picker.open && (
        <PickerShell
          title={t("icon.pickTitle")}
          categories={categories}
          activeCategory={activePack}
          onCategoryChange={picker.setCategory}
          searchPlaceholder={t("icon.searchPlaceholder", String(ALL_NAMES.length))}
          query={picker.query}
          onQueryChange={picker.setQuery}
          onClose={picker.closePicker}
          footer={t("icon.footer")}
        >
          <div style={{ display: "grid", gridTemplateColumns: "repeat(8, 1fr)", gap: 4 }}>
            {entries.map(({ packId, name }) => (
              <IconChoice
                key={`${packId}/${name}`}
                packId={packId}
                name={name}
                color={effectiveColor}
                onPick={async () => {
                  const uri = packId === LUCIDE_PACK_ID
                    ? await lucideToDataUri(name, effectiveColor)
                    : await pluginIconToDataUri(packId, name, effectiveColor);
                  if (uri) onChange(uri, name);
                  picker.closePicker();
                }}
              />
            ))}
            {entries.length === 0 && (
              <div style={{ gridColumn: "1 / -1", color: "var(--ms-text-secondary)", fontSize: 12, padding: 8 }}>{t("icon.noMatch")}</div>
            )}
          </div>
        </PickerShell>
      )}
    </div>
  );
}

function IconChoice({ packId, name, color, onPick }: { packId: string; name: string; color: string; onPick: () => void }) {
  const [uri, setUri] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    const load = packId === LUCIDE_PACK_ID ? lucideToDataUri(name, color) : pluginIconToDataUri(packId, name, color);
    load.then((u) => { if (!cancelled) setUri(u); });
    return () => { cancelled = true; };
  }, [packId, name, color]);

  return (
    <button
      type="button"
      className="ghost"
      title={name}
      onClick={onPick}
      style={{ display: "flex", alignItems: "center", justifyContent: "center", height: 36, padding: 4 }}
    >
      {uri ? <img src={uri} width={20} height={20} alt="" /> : <span style={{ fontSize: 9, opacity: 0.5 }}>…</span>}
    </button>
  );
}

export { iconToDataUri };
