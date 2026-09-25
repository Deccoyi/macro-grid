import { useEffect, useState } from "react";
import type { Page } from "@macro/renderer";
import { api } from "../api/client";
import type { ProfileSummary } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { useBackdropClose } from "../components/useBackdropClose";

interface MoveCopyDialogProps {
  title: string;
  profiles: ProfileSummary[];
  currentProfileId: string;
  currentProfilePages: Page[];
  /** When true, this is "copy a whole page to another profile" — no target-page picker, no Move (moving
   * a whole page out of its profile is just "delete after copying", already its own separate action). */
  wholePage?: boolean;
  onClose: () => void;
  onConfirm: (targetProfileId: string, targetPageId: string, mode: "move" | "copy") => void;
}

/** Modal for "move/copy N widgets" and "copy this page" — pick a target profile (+ page), then Kopyala
 * or Move. A non-active profile's pages are fetched on demand since the editor never holds two
 * profiles in memory at once. */
export function MoveCopyDialog({ title, profiles, currentProfileId, currentProfilePages, wholePage, onClose, onConfirm }: MoveCopyDialogProps) {
  const { t } = useT();
  const backdrop = useBackdropClose(onClose);
  const [targetProfileId, setTargetProfileId] = useState(currentProfileId);
  const [pages, setPages] = useState<Page[]>(currentProfilePages);
  const [targetPageId, setTargetPageId] = useState(currentProfilePages[0]?.id ?? "");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (targetProfileId === currentProfileId) {
      setPages(currentProfilePages);
      setTargetPageId(currentProfilePages[0]?.id ?? "");
      return;
    }
    let cancelled = false;
    setLoading(true);
    api.getProfile(targetProfileId).then((p) => {
      if (cancelled) return;
      setPages(p.pages);
      setTargetPageId(p.pages[0]?.id ?? "");
      setLoading(false);
    });
    return () => { cancelled = true; };
  }, [targetProfileId, currentProfileId, currentProfilePages]);

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,.5)", zIndex: 80, display: "flex", alignItems: "center", justifyContent: "center" }} {...backdrop}>
      <div
        onClick={(e) => e.stopPropagation()}
        style={{ width: 340, background: "var(--ms-bg-surface)", border: "1px solid var(--ms-border)", borderRadius: 10, boxShadow: "0 24px 60px rgba(0,0,0,.5)" }}
      >
        <div style={{ padding: "16px 18px 4px", fontSize: 14, fontWeight: 600 }}>{title}</div>
        <div style={{ padding: "8px 18px 18px", display: "flex", flexDirection: "column", gap: 10 }}>
          <label className="field">
            {t("moveCopy.targetProfile")}
            <select value={targetProfileId} onChange={(e) => setTargetProfileId(e.target.value)}>
              {profiles.map((p) => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
          </label>
          {!wholePage && (
            <label className="field">
              {t("moveCopy.targetPage")}
              <select value={targetPageId} onChange={(e) => setTargetPageId(e.target.value)} disabled={loading || pages.length === 0}>
                {pages.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
            </label>
          )}
        </div>
        <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, padding: "12px 18px", borderTop: "1px solid var(--ms-border)" }}>
          <button className="ghost" onClick={onClose}>{t("moveCopy.cancel")}</button>
          {!wholePage && (
            <button
              className="ghost"
              disabled={!targetPageId}
              onClick={() => onConfirm(targetProfileId, targetPageId, "copy")}
            >
              {t("moveCopy.copy")}
            </button>
          )}
          <button
            className="primary"
            disabled={!wholePage && !targetPageId}
            onClick={() => onConfirm(targetProfileId, targetPageId, wholePage ? "copy" : "move")}
          >
            {wholePage ? t("moveCopy.copy") : t("moveCopy.move")}
          </button>
        </div>
      </div>
    </div>
  );
}
