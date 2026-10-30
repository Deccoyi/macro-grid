import { TriangleAlert } from "lucide-react";
import { isSafeWebUrl } from "@macro/renderer";
import { DismissibleNote, Field, Switch, TextInput } from "./controls";
import { useT } from "../../i18n/I18nContext";
import type { FieldGroupProps } from "./AppearanceFields";

/** The hint under the address of a web page (the widget runs on the phone, so the address has to be one the person trusts), which can be closed for good,
 * and, always visible, what is wrong with the address that is typed now. */
export function WebWarning({ url }: { url: string }) {
  const { t } = useT();
  const isHttp = /^http:\/\//i.test(url);
  const refused = url !== "" && !isSafeWebUrl(url);
  return (
    <>
      <DismissibleNote id="web.warning">
        <strong>{t("fields.web.warnTitle")}</strong>
        <span>{t("fields.web.warn")}</span>
        <span>{t("fields.web.warnSecret")}</span>
        <span style={{ color: "var(--ms-text-secondary)" }}>{t("fields.web.warnLimits")}</span>
      </DismissibleNote>
      {/* About this address, so never closable. */}
      {(isHttp || refused) && (
        <div role="alert" className="pf-note">
          <TriangleAlert size={14} />
          <span>
            {isHttp && <span>{t("fields.web.warnHttp")} </span>}
            {refused && <strong style={{ color: "var(--ms-danger)" }}>{t("fields.web.refused")}</strong>}
          </span>
        </div>
      )}
    </>
  );
}

/** The most web widgets a page can have before the editor warns: what a strong phone is recommended to run at once (a small phone is recommended 1). */
export const WEB_WIDGETS_WARN_ABOVE = 3;

/** For "web" widgets: the address to embed and a name for buttons to pick it by. ("plugin-html" shares this panel until it has its own.) */
export function WebFields({ widget, onChange, webWidgetsOnPage = 0 }: FieldGroupProps & { webWidgetsOnPage?: number }) {
  const { t } = useT();
  const url = typeof widget.props?.url === "string" ? widget.props.url : "";
  const isWeb = widget.type === "web";

  return (
    <>
      {isWeb && (
        <DismissibleNote id="web.experimental" accent="var(--ms-warning, #facc15)">
          <span><strong>{t("fields.web.experimentalTitle")}</strong> {t("fields.web.experimentalText")}</span>
        </DismissibleNote>
      )}
      <Field label={t("fields.web.url")}>
        <TextInput value={url} onChange={(v) => onChange((w) => { w.props = { ...(w.props ?? {}), url: v }; })} placeholder={t("fields.web.urlPlaceholder")} />
      </Field>
      {isWeb && (
        <Field inline label={t("fields.web.keepLoaded")} hint={t("fields.web.keepLoadedHint")}>
          <Switch checked={widget.props?.keepLoaded === true} onChange={(v) => onChange((w) => { w.props = { ...(w.props ?? {}), keepLoaded: v ? true : undefined }; })} />
        </Field>
      )}
      {isWeb && webWidgetsOnPage > WEB_WIDGETS_WARN_ABOVE && (
        <div role="alert" className="pf-note">
          <TriangleAlert size={14} />
          <span>{t("fields.web.manyOnPage", String(webWidgetsOnPage), String(WEB_WIDGETS_WARN_ABOVE))}</span>
        </div>
      )}
      {isWeb ? (
        <WebWarning url={url} />
      ) : (
        <p className="pf-hint">{t("fields.web.note")}</p>
      )}
    </>
  );
}
