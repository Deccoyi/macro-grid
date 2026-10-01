import { useEffect } from "react";
import type { Widget } from "@macro/renderer";
import { SELF_PREVIEW_STATES, usesSelfVariables, type SelfPreviewState } from "../../grid/selfPreview";
import { useT } from "../../i18n/I18nContext";
import { setSelfPreview, useSelfPreview } from "../../state/selfPreviewStore";

/** Shown only for a button whose text or rules use a "This button" variable: picks which of its looks the canvas draws. Not saved with the profile. */
export function SelfPreviewField({ widget }: { widget: Widget }) {
  const { t } = useT();
  const preview = useSelfPreview();
  const uses = usesSelfVariables(widget);

  // Another widget was selected: back to the resting look.
  useEffect(() => () => setSelfPreview(null), [widget.id]);

  if (!uses) return null;
  const value = preview?.widgetId === widget.id ? preview.state : "normal";
  return (
    <label className="field">
      {t("fields.selfPreview.label")}
      <select value={value} onChange={(e) => setSelfPreview({ widgetId: widget.id, state: e.target.value as SelfPreviewState })}>
        {SELF_PREVIEW_STATES.map((s) => <option key={s} value={s}>{t(`fields.selfPreview.${s}`)}</option>)}
      </select>
    </label>
  );
}
