import { useEffect } from "react";
import { useT } from "../i18n/I18nContext";
import { useDocumentTitle } from "../i18n/useDocumentTitle";
import { SafeMarkdown } from "../components/SafeMarkdown";
import { SectionLabel } from "../panels/fields/controls";
import { useUpdate } from "../state/useUpdate";

/** Every button in the footer: one line, one height, whatever the language or the length of its text. */
const BUTTON = { whiteSpace: "nowrap", minHeight: 32, padding: "6px 14px" } as const;

/** The whole page of the "Update" tool window (see ToolWindow.cs): the running and the new version, the notes of every
 * release in between and the person's choices. The tray notification and the status-bar item both open it. */
export function UpdateWindow() {
  const { t, lang } = useT();
  useDocumentTitle("update.title");
  const { snapshot, checking, outcome, check, install, snooze, skip } = useUpdate();

  // Opened from the Help menu's "Check for Updates": look right away instead of showing the last result.
  useEffect(() => {
    if (new URLSearchParams(location.search).get("check")) void check();
  }, [check]);

  if (!snapshot) return <div style={{ padding: 20, fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("app.loading")}</div>;

  const available = snapshot.available;
  const dateFormat = lang === "en" ? "en-US" : "tr-TR";
  const installState = snapshot.install?.state ?? "idle";
  const busy = installState === "downloading" || installState === "starting";

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%", background: "var(--ms-bg-canvas)", color: "var(--ms-text-primary)" }}>
      <div style={{ padding: "16px 20px 10px", borderBottom: "1px solid var(--ms-border)" }}>
        <div style={{ fontSize: 15, fontWeight: 600 }}>
          {available ? t("update.available", available.version) : t("update.upToDate")}
        </div>
        <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 2 }}>{t("update.current", snapshot.currentVersion)}</div>
        {available?.skipped && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 6 }}>{t("update.skipped")}</div>}
        {installState === "downloading" && (
          <div style={{ marginTop: 8 }}>
            <div style={{ fontSize: 12 }}>{t("update.downloading", String(snapshot.install?.percent ?? 0))}</div>
            <progress value={snapshot.install?.percent ?? 0} max={100} style={{ width: "100%", marginTop: 4 }} />
          </div>
        )}
        {installState === "starting" && <div style={{ fontSize: 12, marginTop: 8 }}>{t("update.starting")}</div>}
        {installState === "cancelled" && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 8 }}>{t("update.cancelled")}</div>}
        {installState === "failed" && snapshot.install?.error && (
          <div style={{ fontSize: 12, color: "var(--ms-danger)", marginTop: 8 }}>{t(`update.error.${snapshot.install.error}`)}</div>
        )}
        {available && !available.canInstall && installState === "idle" && (
          <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 6 }}>{t("update.manualOnly")}</div>
        )}
        {!available && outcome === "failed" && <div style={{ fontSize: 12, color: "var(--ms-danger)", marginTop: 6 }}>{t("update.checkFailed")}</div>}
      </div>

      <div style={{ flex: 1, overflowY: "auto", padding: "10px 20px" }}>
        {available?.releases.map((release) => (
          <section key={release.version} style={{ marginBottom: 14 }}>
            <SectionLabel>{t("update.release", release.version)}</SectionLabel>
            {release.publishedAt && (
              <div style={{ fontSize: 11, color: "var(--ms-text-disabled)", margin: "2px 0 6px" }}>
                {new Date(release.publishedAt).toLocaleDateString(dateFormat)}
              </div>
            )}
            {release.notes.trim() ? <SafeMarkdown text={release.notes} /> : <p style={{ fontSize: 12, color: "var(--ms-text-secondary)", margin: 0 }}>{t("update.noNotes")}</p>}
          </section>
        ))}
      </div>

      <div style={{ display: "flex", alignItems: "center", flexWrap: "wrap", gap: 8, padding: "10px 20px", borderTop: "1px solid var(--ms-border)", background: "var(--ms-bg-surface)" }}>
        {available ? (
          <>
            {available.pageUrl && (
              <a href={available.pageUrl} target="_blank" rel="noreferrer" style={{ fontSize: 12, color: "var(--ms-accent, #60a5fa)", whiteSpace: "nowrap" }}>
                {t("update.openReleasePage")}
              </a>
            )}
            <div style={{ flex: 1 }} />
            {!available.skipped && (
              <button type="button" style={BUTTON} onClick={() => void skip().then(() => window.close())} disabled={busy}>{t("update.skip")}</button>
            )}
            <button type="button" style={BUTTON} onClick={() => void snooze().then(() => window.close())} disabled={busy}>{t("update.later")}</button>
            {available.canInstall && (
              <button type="button" className="primary" style={BUTTON} onClick={() => void install()} disabled={busy}>{t("update.install")}</button>
            )}
          </>
        ) : (
          <div style={{ flex: 1 }} />
        )}
        <button type="button" style={BUTTON} onClick={() => void check()} disabled={checking || busy}>
          {checking ? t("update.checking") : t("update.checkNow")}
        </button>
      </div>
    </div>
  );
}
