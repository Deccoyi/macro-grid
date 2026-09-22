import { useEffect, useMemo, useState } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import dynamicIconImports from "lucide-react/dynamicIconImports";
import { useT } from "../i18n/I18nContext";
import { PickerShell, usePickerOpenState } from "./PickerShell";

const ALL_NAMES = Object.keys(dynamicIconImports).sort();

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

/**
 * Icon "packs" the picker lists on the left, exactly like the variable picker lists categories.
 * Only lucide ships today; a future icon-pack plugin would add another entry here (and its own
 * name -> render function), without changing anything about how the picker itself works.
 */
const ICON_PACKS = [{ id: "lucide", label: "Lucide", badge: ALL_NAMES.length }];

async function iconToDataUri(name: string, color: string): Promise<string | null> {
  const load = dynamicIconImports[name as keyof typeof dynamicIconImports];
  if (!load) return null;
  const mod = await load();
  const Icon = mod.default;
  const svg = renderToStaticMarkup(<Icon color={color} size={24} strokeWidth={2} />);
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

export interface IconPickerProps {
  /** The widget's current icon, as a data: URI (what WidgetStyle.icon holds). */
  value?: string;
  /** Baked into the generated SVG's stroke color at pick time — usually the widget's own foreground. */
  color?: string;
  onChange: (dataUri: string | undefined, iconName: string | undefined) => void;
}

/** "İkon seç…" button + a search modal over every lucide-react icon (ISC licensed, bundled locally — no network call). */
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

  const names = useMemo(() => {
    const q = picker.query.trim().toLowerCase();
    const source = q ? ALL_NAMES.filter((n) => n.includes(q)) : POPULAR;
    return source.slice(0, MAX_RESULTS);
  }, [picker.query]);

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
          categories={ICON_PACKS}
          activeCategory="lucide"
          onCategoryChange={() => {}}
          searchPlaceholder={t("icon.searchPlaceholder", String(ALL_NAMES.length))}
          query={picker.query}
          onQueryChange={picker.setQuery}
          onClose={picker.closePicker}
          footer={t("icon.footer")}
        >
          <div style={{ display: "grid", gridTemplateColumns: "repeat(8, 1fr)", gap: 4 }}>
            {names.map((name) => (
              <IconChoice
                key={name}
                name={name}
                color={effectiveColor}
                onPick={async () => {
                  const uri = await iconToDataUri(name, effectiveColor);
                  if (uri) onChange(uri, name);
                  picker.closePicker();
                }}
              />
            ))}
            {names.length === 0 && (
              <div style={{ gridColumn: "1 / -1", color: "var(--ms-text-secondary)", fontSize: 12, padding: 8 }}>{t("icon.noMatch")}</div>
            )}
          </div>
        </PickerShell>
      )}
    </div>
  );
}

function IconChoice({ name, color, onPick }: { name: string; color: string; onPick: () => void }) {
  const [uri, setUri] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    iconToDataUri(name, color).then((u) => { if (!cancelled) setUri(u); });
    return () => { cancelled = true; };
  }, [name, color]);

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
