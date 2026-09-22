import { useState } from "react";
import { Trash2 } from "lucide-react";
import { useT } from "../i18n/I18nContext";
import { SectionLabel, Seg } from "../panels/fields/controls";
import { usePreferences } from "../preferences/PreferencesContext";
import { ToolWindowLayout } from "./ToolWindowLayout";

type Category = "appearance" | "language" | "previewProfiles";

/** The whole page of the "Tercihler" tool window (see ToolWindow.cs) — a real separate, non-modal OS
 * window, not an in-page dialog. */
export function PreferencesWindow() {
  const { t, lang, setLang } = useT();
  const { theme, setTheme, previewProfiles, addPreviewProfile, removePreviewProfile } = usePreferences();
  const [category, setCategory] = useState<Category>("appearance");
  const [name, setName] = useState("");
  const [width, setWidth] = useState(390);
  const [height, setHeight] = useState(844);

  const categories = [
    { id: "appearance", label: t("preferences.category.appearance") },
    { id: "language", label: t("preferences.category.language") },
    { id: "previewProfiles", label: t("preferences.category.previewProfiles") },
  ];

  return (
    <ToolWindowLayout categories={categories} activeId={category} onSelect={(id) => setCategory(id as Category)}>
      {category === "appearance" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 360 }}>
          <SectionLabel>{t("preferences.category.appearance")}</SectionLabel>
          <label className="field">
            {t("preferences.theme")}
            <Seg
              value={theme}
              onChange={setTheme}
              options={[
                { value: "dark", label: t("preferences.theme.dark") },
                { value: "light", label: t("preferences.theme.light") },
              ]}
            />
          </label>
        </div>
      )}

      {category === "language" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 360 }}>
          <SectionLabel>{t("preferences.category.language")}</SectionLabel>
          <label className="field">
            {t("preferences.language")}
            <Seg value={lang} onChange={setLang} options={[{ value: "tr", label: "Türkçe" }, { value: "en", label: "English" }]} />
          </label>
        </div>
      )}

      {category === "previewProfiles" && (
        <div style={{ maxWidth: 420 }}>
          <SectionLabel>{t("preferences.previewProfiles")}</SectionLabel>
          {previewProfiles.map((p) => (
            <div key={p.id} style={{ display: "flex", alignItems: "center", gap: 8, padding: "6px 0", borderTop: "1px solid var(--ms-border)" }}>
              <div style={{ flex: 1, fontSize: 12.5 }}>{p.name}</div>
              <div style={{ fontSize: 11, color: "var(--ms-text-secondary)" }}>{p.width}×{p.height}</div>
              <button type="button" className="ghost" title={t("preferences.previewProfiles.remove")} onClick={() => removePreviewProfile(p.id)} style={{ padding: 5, color: "var(--ms-danger)" }}>
                <Trash2 size={13} />
              </button>
            </div>
          ))}
          <div style={{ display: "flex", gap: 6, marginTop: 8 }}>
            <input type="text" placeholder={t("preferences.previewProfiles.name")} value={name} onChange={(e) => setName(e.target.value)} style={{ flex: 1 }} />
            <input type="number" min={100} max={4000} value={width} onChange={(e) => setWidth(Number(e.target.value))} style={{ width: 60 }} title={t("preferences.previewProfiles.width")} />
            <input type="number" min={100} max={4000} value={height} onChange={(e) => setHeight(Number(e.target.value))} style={{ width: 60 }} title={t("preferences.previewProfiles.height")} />
            <button
              type="button"
              className="ghost"
              disabled={!name.trim()}
              onClick={() => { addPreviewProfile({ name: name.trim(), width, height }); setName(""); }}
            >
              {t("preferences.previewProfiles.add")}
            </button>
          </div>
        </div>
      )}
    </ToolWindowLayout>
  );
}
