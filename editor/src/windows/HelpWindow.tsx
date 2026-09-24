import { useEffect, useState } from "react";
import { api, type LegalOverview } from "../api/client";
import { useT } from "../i18n/I18nContext";
import { useServerVersion } from "../state/useServerVersion";
import { SectionLabel } from "../panels/fields/controls";
import { ToolWindowLayout } from "./ToolWindowLayout";

type Category = "about" | "agreement" | "licenses";

const isCategory = (value: string | null): value is Category => value === "about" || value === "agreement" || value === "licenses";

const textStyle = {
  margin: 0,
  fontFamily: "Consolas, 'Cascadia Mono', monospace",
  fontSize: 12,
  lineHeight: 1.5,
  whiteSpace: "pre-wrap",
  overflowWrap: "anywhere",
  color: "var(--ms-text-primary)",
} as const;

/** The whole page of the "Yardım" tool window (see ToolWindow.cs) — a real separate OS window, not an in-page dialog.
 * The legal texts come from the files that ship next to the exe (GET /api/legal), so the app itself shows what it is
 * licensed under and what the person agreed to, instead of pointing at a repository. */
export function HelpWindow() {
  const { t } = useT();
  const serverVersion = useServerVersion();
  const requested = new URLSearchParams(location.search).get("tab");
  const [category, setCategory] = useState<Category>(isCategory(requested) ? requested : "about");
  const [legal, setLegal] = useState<LegalOverview | null>(null);
  const [failed, setFailed] = useState(false);
  const [library, setLibrary] = useState<string | null>(null);
  const [libraryText, setLibraryText] = useState<string>("");

  useEffect(() => {
    api.getLegal().then(setLegal).catch(() => setFailed(true));
  }, []);

  useEffect(() => {
    if (!library) return;
    setLibraryText("");
    api.getLegalLibrary(library).then(setLibraryText).catch(() => setLibraryText(t("help.unavailable")));
  }, [library, t]);

  const categories = [
    { id: "about", label: t("menu.help.about") },
    { id: "agreement", label: t("menu.help.agreement") },
    { id: "licenses", label: t("menu.help.licenses") },
  ];

  const unavailable = <p style={{ ...textStyle, color: "var(--ms-text-secondary)" }}>{failed ? t("help.unavailable") : "…"}</p>;

  return (
    <ToolWindowLayout categories={categories} activeId={category} onSelect={(id) => setCategory(id as Category)}>
      {category === "about" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 14, maxWidth: 560 }}>
          <SectionLabel>{t("menu.help.version", serverVersion)}</SectionLabel>
          <p style={{ ...textStyle, fontFamily: "inherit", fontSize: 13 }}>{t("help.about.aiNotice")}</p>
          <p style={{ ...textStyle, fontFamily: "inherit", fontSize: 13 }}>{t("help.about.noWarranty")}</p>
          <p style={{ ...textStyle, fontFamily: "inherit", fontSize: 13 }}>{t("help.about.risk")}</p>
        </div>
      )}

      {category === "agreement" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <SectionLabel>{t("menu.help.agreement")}</SectionLabel>
          {legal?.agreement ? <pre style={textStyle}>{legal.agreement}</pre> : legal ? <p style={textStyle}>{t("help.unavailable")}</p> : unavailable}
        </div>
      )}

      {category === "licenses" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <SectionLabel>{t("menu.help.licenses")}</SectionLabel>
          {!legal ? unavailable : (
            <>
              {legal.projectLicense && (
                <details>
                  <summary style={{ cursor: "pointer", fontSize: 13 }}>{t("help.projectLicense")}</summary>
                  <pre style={{ ...textStyle, marginTop: 8 }}>{legal.projectLicense}</pre>
                </details>
              )}
              {legal.notices && (
                <details>
                  <summary style={{ cursor: "pointer", fontSize: 13 }}>{t("help.thirdPartyNotices")}</summary>
                  <pre style={{ ...textStyle, marginTop: 8 }}>{legal.notices}</pre>
                </details>
              )}
              {legal.libraries.length === 0 && !legal.projectLicense && !legal.notices && <p style={textStyle}>{t("help.unavailable")}</p>}
              {legal.libraries.length > 0 && (
                <>
                  <p style={{ ...textStyle, fontFamily: "inherit", color: "var(--ms-text-secondary)" }}>{t("help.libraries")}</p>
                  <select value={library ?? ""} onChange={(e) => setLibrary(e.target.value || null)} style={{ maxWidth: 360 }}>
                    <option value="">{t("help.libraries.choose")}</option>
                    {legal.libraries.map((name) => (
                      <option key={name} value={name}>{name}</option>
                    ))}
                  </select>
                  {library && <pre style={textStyle}>{libraryText}</pre>}
                </>
              )}
            </>
          )}
        </div>
      )}
    </ToolWindowLayout>
  );
}
