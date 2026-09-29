import { isSafeWebUrl } from "@macro/renderer";
import { useT } from "../../i18n/I18nContext";
import type { FieldGroupProps } from "./AppearanceFields";

/** The always-visible warning under the address of a web page: the widget runs on the phone, so the address has to be one the person trusts. */
export function WebWarning({ url }: { url: string }) {
  const { t } = useT();
  const isHttp = /^http:\/\//i.test(url);
  const refused = url !== "" && !isSafeWebUrl(url);
  return (
    <div
      role="note"
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 4,
        padding: "8px 10px",
        fontSize: 11.5,
        lineHeight: 1.4,
        color: "var(--ms-text-primary)",
        background: "var(--ms-accent-bg-muted)",
        borderLeft: "3px solid var(--ms-accent)",
        borderRadius: 4,
      }}
    >
      <strong>{t("fields.web.warnTitle")}</strong>
      <span>{t("fields.web.warn")}</span>
      <span>{t("fields.web.warnSecret")}</span>
      {isHttp && <span>{t("fields.web.warnHttp")}</span>}
      <span style={{ color: "var(--ms-text-secondary)" }}>{t("fields.web.warnLimits")}</span>
      {refused && <span style={{ color: "var(--ms-danger)", fontWeight: 600 }}>{t("fields.web.refused")}</span>}
    </div>
  );
}

/** For "web" widgets: the address to embed and a name for buttons to pick it by. ("plugin-html" shares this panel until it has its own.) */
export function WebFields({ widget, onChange }: FieldGroupProps) {
  const { t } = useT();
  const url = typeof widget.props?.url === "string" ? widget.props.url : "";
  const isWeb = widget.type === "web";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
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
      {isWeb ? (
        <WebWarning url={url} />
      ) : (
        <p style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: 0 }}>{t("fields.web.note")}</p>
      )}
    </div>
  );
}
