import { useT } from "../i18n/I18nContext";

/** The body of the "Back up everything" dialog: what the file holds, and what it leaves out. */
export function BackupContent() {
  const { t } = useT();
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10, fontSize: 12.5, color: "var(--ms-text-secondary)" }}>
      <p style={{ margin: 0 }}>{t("backup.intro")}</p>
      <p style={{ margin: 0 }}>{t("backup.note")}</p>
    </div>
  );
}
