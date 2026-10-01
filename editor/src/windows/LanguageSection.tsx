import { useCallback, useEffect, useState } from "react";
import { api } from "../api/client";
import type { LanguagePackFile, LanguagePackInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { canonicalTag } from "../i18n/language";
import { ensurePack } from "../i18n/packStore";
import { buildTemplate, coverage, importTable, type ImportReport } from "../i18n/pack/template";
import { checkPack } from "../i18n/pack/validate";
import { SectionLabel, Seg } from "../panels/fields/controls";
import type { DictKey } from "../i18n/tr";

const REPORT_ROWS_SHOWN = 8;
const muted = { fontSize: 11.5, color: "var(--ms-text-secondary)" } as const;

interface PendingImport {
  pack: LanguagePackFile;
  report: ImportReport;
}

interface PackRow extends LanguagePackInfo {
  done: number;
  total: number;
  needsReview: number;
}

/** The Language page of the Preferences window: the two built-in languages, the installed language packs, and a table export/import for making a new one. */
export function LanguageSection() {
  const { t, tn, lang, setLang } = useT();
  const [packs, setPacks] = useState<PackRow[]>([]);
  const [removing, setRemoving] = useState<string | null>(null);
  const [tag, setTag] = useState("");
  const [name, setName] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [pending, setPending] = useState<PendingImport | null>(null);

  const reload = useCallback(async () => {
    try {
      const list = await api.listLanguagePacks();
      const rows = await Promise.all(
        list.map(async (info): Promise<PackRow> => {
          try {
            const file = await api.getLanguagePack(info.tag);
            const stats = coverage(checkPack(file, info.tag)?.strings ?? {}, file.sources);
            return { ...info, ...stats };
          } catch {
            return { ...info, done: 0, total: 0, needsReview: 0 };
          }
        }),
      );
      setPacks(rows);
    } catch {
      setPacks([]);
    }
  }, []);

  useEffect(() => { void reload(); }, [reload]);

  const failed = (err: unknown) => setMessage(t("languagePack.error", err instanceof Error ? err.message : String(err)));

  const exportTable = async (packTag: string) => {
    setMessage(null);
    try {
      let existing: Record<string, string> | undefined;
      try {
        existing = checkPack(await api.getLanguagePack(packTag), packTag)?.strings;
      } catch { /* a new language has no pack yet */ }
      await api.exportLanguageCsvDialog(`${packTag}.csv`, buildTemplate(existing));
    } catch (err) {
      failed(err);
    }
  };

  const importFor = async (packTag: string, packName: string, version: number) => {
    setMessage(null);
    setPending(null);
    try {
      const picked = await api.importLanguageCsvDialog();
      if (picked.path === null || picked.text === undefined) return;
      const outcome = importTable(picked.text, packTag, packName, version + 1);
      if (!outcome.ok) setMessage(t(`languagePack.import.${outcome.reason}` as DictKey));
      else setPending({ pack: outcome.pack, report: outcome.report });
    } catch (err) {
      failed(err);
    }
  };

  const savePending = async () => {
    if (!pending) return;
    try {
      await api.saveLanguagePack(pending.pack.meta.tag, pending.pack);
      if (lang === pending.pack.meta.tag) await ensurePack(lang);
      setPending(null);
      await reload();
    } catch (err) {
      failed(err);
    }
  };

  const removePack = async (packTag: string) => {
    setRemoving(null);
    try {
      if (lang === packTag) setLang("en");
      await api.deleteLanguagePack(packTag);
      await reload();
    } catch (err) {
      failed(err);
    }
  };

  const newTag = canonicalTag(tag);
  const tagInvalid = tag.trim() !== "" && newTag === null;
  const known = packs.find((p) => p.tag === newTag);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 14, maxWidth: 440 }}>
      <SectionLabel>{t("preferences.category.language")}</SectionLabel>
      <p style={{ ...muted, margin: 0 }}>{t("languagePack.intro")}</p>

      <label className="field">
        {t("languagePack.builtIn")}
        <Seg value={lang} onChange={setLang} options={[{ value: "tr", label: "Türkçe" }, { value: "en", label: "English" }]} />
      </label>

      <div>
        <SectionLabel>{t("languagePack.packs")}</SectionLabel>
        {packs.length === 0 && <div style={muted}>{t("languagePack.none")}</div>}
        {packs.map((p) => (
          <div key={p.tag} style={{ display: "flex", flexDirection: "column", gap: 4, padding: "8px 0", borderTop: "1px solid var(--ms-border)" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <div style={{ flex: 1, fontSize: 12.5 }}>{p.name} <span style={muted}>({p.tag})</span></div>
              {lang === p.tag ? (
                <span style={{ ...muted, color: "var(--ms-accent)" }}>{t("languagePack.active")}</span>
              ) : (
                <button type="button" onClick={() => setLang(p.tag)}>{t("languagePack.use")}</button>
              )}
            </div>
            <div style={muted}>
              {t("languagePack.coverage", String(p.done), String(p.total))}
              {p.needsReview > 0 && ` · ${tn("languagePack.needsReview", p.needsReview)}`}
            </div>
            {removing === p.tag ? (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                <div style={{ fontSize: 12 }}>{t("languagePack.removeConfirm", p.name)}</div>
                <div style={{ display: "flex", gap: 6 }}>
                  <button type="button" onClick={() => void removePack(p.tag)} style={{ color: "var(--ms-danger)" }}>{t("languagePack.remove")}</button>
                  <button type="button" className="ghost" onClick={() => setRemoving(null)}>{t("languagePack.cancel")}</button>
                </div>
              </div>
            ) : (
              <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                <button type="button" className="ghost" onClick={() => void exportTable(p.tag)}>{t("languagePack.export")}</button>
                <button type="button" className="ghost" onClick={() => void importFor(p.tag, p.name, p.version)}>{t("languagePack.import")}</button>
                <button type="button" className="ghost" onClick={() => setRemoving(p.tag)} style={{ color: "var(--ms-danger)" }}>{t("languagePack.remove")}</button>
              </div>
            )}
          </div>
        ))}
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 6, borderTop: "1px solid var(--ms-border)", paddingTop: 10 }}>
        <SectionLabel>{t("languagePack.new")}</SectionLabel>
        <input type="text" placeholder={t("languagePack.tag")} value={tag} onChange={(e) => setTag(e.target.value)} maxLength={35} />
        <input type="text" placeholder={t("languagePack.name")} value={name} onChange={(e) => setName(e.target.value)} maxLength={40} />
        {tagInvalid && <div style={{ ...muted, color: "var(--ms-danger)" }}>{t("languagePack.tagInvalid")}</div>}
        <div style={{ display: "flex", gap: 6 }}>
          <button type="button" disabled={newTag === null} onClick={() => newTag && void exportTable(newTag)}>{t("languagePack.export")}</button>
          <button
            type="button"
            disabled={newTag === null || name.trim() === ""}
            onClick={() => newTag && void importFor(newTag, known?.name ?? name, known?.version ?? 0)}
          >
            {t("languagePack.import")}
          </button>
        </div>
      </div>

      {message && <div role="alert" style={{ ...muted, color: "var(--ms-danger)" }}>{message}</div>}

      {pending && (
        <div role="region" style={{ display: "flex", flexDirection: "column", gap: 6, padding: 10, border: "1px solid var(--ms-border)", borderRadius: 4, background: "var(--ms-bg-surface)" }}>
          <div style={{ fontSize: 12.5, fontWeight: 600 }}>{t("languagePack.report.title", pending.pack.meta.name)}</div>
          <div style={{ fontSize: 12 }}>
            {t("languagePack.report.summary", String(pending.report.accepted), String(pending.report.empty), String(pending.report.refused.length))}
          </div>
          {pending.report.refused.slice(0, REPORT_ROWS_SHOWN).map((row) => (
            <div key={`${row.line}:${row.key}`} style={muted}>
              {t("languagePack.report.row", String(row.line), row.key, t(`languagePack.reason.${row.reason}` as DictKey))}
            </div>
          ))}
          {pending.report.refused.length > REPORT_ROWS_SHOWN && (
            <div style={muted}>{t("languagePack.report.more", String(pending.report.refused.length - REPORT_ROWS_SHOWN))}</div>
          )}
          <div style={{ display: "flex", gap: 6 }}>
            <button type="button" disabled={pending.report.accepted === 0} onClick={() => void savePending()}>{t("languagePack.report.save")}</button>
            <button type="button" className="ghost" onClick={() => setPending(null)}>{t("languagePack.cancel")}</button>
          </div>
        </div>
      )}
    </div>
  );
}
