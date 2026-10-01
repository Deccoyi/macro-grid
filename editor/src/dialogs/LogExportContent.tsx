import { useEffect, useState } from "react";
import { api } from "../api/client";
import { useT } from "../i18n/I18nContext";

/** The body of the "Export logs" dialog: what the file would hold, so nobody saves it blind. */
export function LogExportContent() {
  const { t } = useT();
  const [preview, setPreview] = useState<{ files: { name: string; bytes: number }[]; totalBytes: number } | null | "failed">(null);

  useEffect(() => {
    let stopped = false;
    api.fetchLogsPreview().then((p) => { if (!stopped) setPreview(p); }).catch(() => { if (!stopped) setPreview("failed"); });
    return () => { stopped = true; };
  }, []);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10, fontSize: 12.5, color: "var(--ms-text-secondary)" }}>
      <p style={{ margin: 0 }}>{t("logExport.intro")}</p>
      {preview === null && <p style={{ margin: 0 }}>{t("logExport.loading")}</p>}
      {preview === "failed" && <p style={{ margin: 0, color: "var(--ms-danger)" }}>{t("logExport.failed")}</p>}
      {preview && preview !== "failed" && (
        preview.files.length === 0
          ? <p style={{ margin: 0 }}>{t("logExport.noFiles")}</p>
          : (
            <ul style={{ margin: 0, paddingLeft: 18, fontFamily: "Consolas, monospace", fontSize: 11.5 }}>
              {preview.files.map((f) => <li key={f.name}>{f.name} — {formatSize(f.bytes)}</li>)}
            </ul>
          )
      )}
      <p style={{ margin: 0 }}>{t("logExport.note")}</p>
    </div>
  );
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
