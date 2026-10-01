import { useState } from "react";
import type { InspectBackupResult, RestoreItem, RestoreWarning } from "../api/types";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";

type Inspected = NonNullable<InspectBackupResult["result"]>;
type Translate = (key: DictKey, ...args: string[]) => string;

/** The key an item is ticked by; the dialog's caller reads the ticked set after the person confirms. */
export const restoreItemId = (item: RestoreItem) => `${item.kind}:${item.key}`;

/** The warning in the person's language; a code this editor does not know is shown as it is, never dropped. */
export function restoreWarningText(t: Translate, warning: RestoreWarning): string {
  const key = `restore.warn.${warning.code}` as DictKey;
  return KNOWN_WARNINGS.has(warning.code) ? t(key, ...warning.args) : warning.code;
}

const KNOWN_WARNINGS = new Set([
  "unencryptedKept", "languagePackMissing", "deviceNotPaired", "deviceProfileMissing", "missingPlugin",
  "unknownActionType", "missingFile", "variableTypeConflict", "pluginSettingsNotRunning",
]);

function itemTitle(t: Translate, item: RestoreItem): string {
  switch (item.kind) {
    case "profile": return t("restore.item.profile", item.name);
    case "profileTree": return t("restore.item.profileTree");
    case "preferences": return t("restore.item.preferences");
    case "variables": return t("restore.item.variables");
    case "automation": return t("restore.item.automation");
    case "device": return t("restore.item.device", item.name);
    case "pluginSettings": return t("restore.item.pluginSettings", item.name);
    case "languagePack": return t("restore.item.languagePack", item.name);
  }
}

function itemDetail(t: Translate, item: RestoreItem): string[] {
  const lines: string[] = [];
  const n = (v: number | null | undefined) => String(v ?? 0);
  if (item.kind === "profile") {
    lines.push(item.state === "new"
      ? t("restore.detail.profileNew", n(item.backupPages), n(item.backupWidgets))
      : t("restore.detail.profile", n(item.backupPages), n(item.backupWidgets), n(item.herePages), n(item.hereWidgets)));
    if (item.state === "different" && item.names.length > 0) lines.push(t("restore.detail.changedPages", item.names.join(", ")));
  } else if (item.kind === "variables") {
    lines.push(t("restore.detail.variables", n(item.added), n(item.changed), n(item.skipped)));
  } else if (item.kind === "automation") {
    lines.push(t("restore.detail.automation", n(item.added), n(item.changed)));
  } else if (item.kind === "languagePack") {
    lines.push(t("restore.detail.languagePack", item.hereVersion == null ? "-" : n(item.hereVersion), item.backupVersion == null ? "-" : n(item.backupVersion)));
  } else if (item.state === "different" && item.names.length > 0) {
    lines.push(t("restore.detail.names", item.names.join(", ")));
  }
  return lines;
}

/** The body of the "Restore" dialog: what the backup holds compared with what is here, one tick per item, then what to look out for. */
export function RestoreContent({ inspected, ticked }: { inspected: Inspected; ticked: Set<string> }) {
  const { t } = useT();
  const [, redraw] = useState(0);
  const actionable = inspected.items.filter((i) => i.state !== "same");
  const date = new Date(inspected.manifest.createdAt).toLocaleString();

  const toggle = (item: RestoreItem) => {
    const id = restoreItemId(item);
    if (ticked.has(id)) ticked.delete(id); else ticked.add(id);
    redraw((n) => n + 1);
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10, fontSize: 12.5, color: "var(--ms-text-secondary)", maxHeight: 420, overflowY: "auto" }}>
      <p style={{ margin: 0 }}>{t("restore.madeOn", date, inspected.manifest.serverVersion)}</p>
      {actionable.length === 0
        ? <p style={{ margin: 0 }}>{t("restore.nothing")}</p>
        : (
          <>
            <p style={{ margin: 0 }}>{t("restore.intro")}</p>
            <ul style={{ margin: 0, padding: 0, listStyle: "none", display: "flex", flexDirection: "column", gap: 6 }}>
              {actionable.map((item) => (
                <li key={restoreItemId(item)}>
                  <label style={{ display: "flex", gap: 8, alignItems: "flex-start", cursor: "pointer" }}>
                    <input type="checkbox" checked={ticked.has(restoreItemId(item))} onChange={() => toggle(item)} style={{ marginTop: 2 }} />
                    <span style={{ display: "flex", flexDirection: "column", gap: 2 }}>
                      <span style={{ color: "var(--ms-text)" }}>
                        {itemTitle(t, item)} <em style={{ fontStyle: "normal", opacity: 0.7 }}>({t(`restore.state.${item.state}` as DictKey)})</em>
                      </span>
                      {itemDetail(t, item).map((line, i) => <span key={i} style={{ fontSize: 11.5 }}>{line}</span>)}
                    </span>
                  </label>
                </li>
              ))}
            </ul>
          </>
        )}
      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
        <strong style={{ color: "var(--ms-text)" }}>{t("restore.warnings")}</strong>
        <ul style={{ margin: 0, paddingLeft: 18, fontSize: 11.5 }}>
          <li>{t("restore.warn.passwords")}</li>
          {inspected.warnings.map((w, i) => <li key={i}>{restoreWarningText(t, w)}</li>)}
        </ul>
      </div>
    </div>
  );
}
