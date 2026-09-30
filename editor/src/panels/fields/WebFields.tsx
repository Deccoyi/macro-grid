import { isSafeWebUrl } from "@macro/renderer";
import { DismissibleNote } from "./controls";
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
        <div
          role="alert"
          style={{ display: "flex", flexDirection: "column", gap: 4, padding: "8px 10px", fontSize: 11.5, lineHeight: 1.4, background: "var(--ms-accent-bg-muted)", borderLeft: "3px solid var(--ms-danger)", borderRadius: 4 }}
        >
          {isHttp && <span>{t("fields.web.warnHttp")}</span>}
          {refused && <span style={{ color: "var(--ms-danger)", fontWeight: 600 }}>{t("fields.web.refused")}</span>}
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
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      {isWeb && (
        <DismissibleNote id="web.experimental" accent="var(--ms-warning, #facc15)">
          <span><strong>{t("fields.web.experimentalTitle")}</strong> {t("fields.web.experimentalText")}</span>
        </DismissibleNote>
      )}
      {isWeb && (
        <label className="field">
          {t("fields.web.name")}
          <input
            type="text"
            value={widget.name ?? ""}
            onChange={(e) => onChange((w) => { w.name = e.target.value === "" ? undefined : e.target.value; })}
          />
          <span style={{ fontSize: 11, color: "var(--ms-text-disabled)" }}>{t("fields.web.nameHint")}</span>
        </label>
      )}
      <label className="field">
        {t("fields.web.url")}
        <input
          type="text"
          value={url}
          onChange={(e) => onChange((w) => { w.props = { ...(w.props ?? {}), url: e.target.value }; })}
          placeholder={t("fields.web.urlPlaceholder")}
        />
      </label>
      {isWeb && (
        <label style={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <span style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, color: "var(--ms-text-primary)" }}>
            <input
              type="checkbox"
              checked={widget.props?.keepLoaded === true}
              onChange={(e) => onChange((w) => { w.props = { ...(w.props ?? {}), keepLoaded: e.target.checked ? true : undefined }; })}
            />
            {t("fields.web.keepLoaded")}
          </span>
          <span style={{ marginLeft: 22, fontSize: 11, lineHeight: 1.4, color: "var(--ms-text-secondary)" }}>{t("fields.web.keepLoadedHint")}</span>
        </label>
      )}
      {isWeb && webWidgetsOnPage > WEB_WIDGETS_WARN_ABOVE && (
        <div role="alert" style={{ padding: "8px 10px", fontSize: 11.5, lineHeight: 1.4, background: "var(--ms-accent-bg-muted)", borderLeft: "3px solid var(--ms-warning, #facc15)", borderRadius: 4 }}>
          {t("fields.web.manyOnPage", String(webWidgetsOnPage), String(WEB_WIDGETS_WARN_ABOVE))}
        </div>
      )}
      {isWeb ? (
        <WebWarning url={url} />
      ) : (
        <p style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: 0 }}>{t("fields.web.note")}</p>
      )}
    </div>
  );
}
