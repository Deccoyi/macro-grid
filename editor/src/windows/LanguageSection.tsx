import { useCallback, useEffect, useState } from "react";
import { api } from "../api/client";
import type { LanguagePackFile, LanguagePackInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { canonicalTag } from "../i18n/language";
import { ensurePack } from "../i18n/packStore";
import { buildTemplate, coverage, importTable, type ImportReport } from "../i18n/pack/template";
import { checkPack } from "../i18n/pack/validate";
import { Seg, TextInput } from "../panels/fields/controls";
import { PageHeader, SettingGroup, SettingRow } from "./settingsPrimitives";
import type { DictKey } from "../i18n/tr";

const REPORT_ROWS_SHOWN = 8;

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
    <>
      <PageHeader title={t("preferences.category.language")} lead={t("languagePack.intro")} alert={message} />

      <SettingGroup>
        <SettingRow
          name={t("languagePack.builtIn")}
          control={<Seg value={lang} onChange={setLang} options={[{ value: "tr", label: "Türkçe" }, { value: "en", label: "English" }]} />}
        />
      </SettingGroup>

      <SettingGroup title={t("languagePack.packs")}>
        {packs.length === 0 && <div className="st-row"><div className="st-hint">{t("languagePack.none")}</div></div>}
        {packs.map((p) => (
          <div key={p.tag} className="st-row" style={{ gridTemplateColumns: "minmax(0, 1fr) auto", columnGap: 12, rowGap: 6 }}>
            <div>
              <div className="st-row-name">{p.name} <span className="st-hint">({p.tag})</span></div>
              <div className="st-row-hint">
                {t("languagePack.coverage", String(p.done), String(p.total))}
                {p.needsReview > 0 && ` · ${tn("languagePack.needsReview", p.needsReview)}`}
              </div>
            </div>
            {lang === p.tag ? (
              <span className="st-hint" style={{ color: "var(--ms-accent)" }}>{t("languagePack.active")}</span>
            ) : (
              <button type="button" className="st-btn" onClick={() => setLang(p.tag)}>{t("languagePack.use")}</button>
            )}
            {removing === p.tag ? (
              <div style={{ gridColumn: "1 / -1", display: "flex", flexDirection: "column", gap: 6 }}>
                <div style={{ fontSize: 12 }}>{t("languagePack.removeConfirm", p.name)}</div>
                <div style={{ display: "flex", gap: 8 }}>
                  <button type="button" className="st-btn" onClick={() => void removePack(p.tag)} style={{ color: "var(--ms-danger)" }}>{t("languagePack.remove")}</button>
                  <button type="button" className="st-btn" onClick={() => setRemoving(null)}>{t("languagePack.cancel")}</button>
                </div>
              </div>
            ) : (
              <div style={{ gridColumn: "1 / -1", display: "flex", gap: 8, flexWrap: "wrap" }}>
                <button type="button" className="st-btn" onClick={() => void exportTable(p.tag)}>{t("languagePack.export")}</button>
                <button type="button" className="st-btn" onClick={() => void importFor(p.tag, p.name, p.version)}>{t("languagePack.import")}</button>
                <button type="button" className="st-btn" onClick={() => setRemoving(p.tag)} style={{ color: "var(--ms-danger)" }}>{t("languagePack.remove")}</button>
              </div>
            )}
          </div>
        ))}
      </SettingGroup>

      <SettingGroup title={t("languagePack.new")}>
        <SettingRow name={t("languagePack.tag")} control={<TextInput value={tag} onChange={setTag} maxLength={35} label={t("languagePack.tag")} />} hint={tagInvalid ? <span className="st-hint error">{t("languagePack.tagInvalid")}</span> : undefined} />
        <SettingRow name={t("languagePack.name")} control={<TextInput value={name} onChange={setName} maxLength={40} label={t("languagePack.name")} />} />
      </SettingGroup>
      <div className="st-footer" style={{ display: "flex", gap: 8 }}>
        <button type="button" className="st-btn" disabled={newTag === null} onClick={() => newTag && void exportTable(newTag)}>{t("languagePack.export")}</button>
        <button
          type="button" className="st-btn"
          disabled={newTag === null || name.trim() === ""}
          onClick={() => newTag && void importFor(newTag, known?.name ?? name, known?.version ?? 0)}
        >
          {t("languagePack.import")}
        </button>
      </div>

      {pending && (
        <div role="region" className="st-note" style={{ marginTop: 16, display: "flex", flexDirection: "column", gap: 6, padding: 10 }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: "var(--ms-text-primary)" }}>{t("languagePack.report.title", pending.pack.meta.name)}</div>
          <div style={{ fontSize: 12 }}>
            {t("languagePack.report.summary", String(pending.report.accepted), String(pending.report.empty), String(pending.report.refused.length))}
          </div>
          {pending.report.refused.slice(0, REPORT_ROWS_SHOWN).map((row) => (
            <div key={`${row.line}:${row.key}`} className="st-hint">
              {t("languagePack.report.row", String(row.line), row.key, t(`languagePack.reason.${row.reason}` as DictKey))}
            </div>
          ))}
          {pending.report.refused.length > REPORT_ROWS_SHOWN && (
            <div className="st-hint">{t("languagePack.report.more", String(pending.report.refused.length - REPORT_ROWS_SHOWN))}</div>
          )}
          <div style={{ display: "flex", gap: 8 }}>
            <button type="button" className="st-btn" disabled={pending.report.accepted === 0} onClick={() => void savePending()}>{t("languagePack.report.save")}</button>
            <button type="button" className="st-btn" onClick={() => setPending(null)}>{t("languagePack.cancel")}</button>
          </div>
        </div>
      )}
    </>
  );
}
