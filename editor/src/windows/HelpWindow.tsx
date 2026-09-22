import { useState } from "react";
import { useT } from "../i18n/I18nContext";
import { SectionLabel } from "../panels/fields/controls";
import { ToolWindowLayout } from "./ToolWindowLayout";

type Category = "licenses" | "agreement";

const APP_VERSION = "0.1.0";

/** The whole page of the "Yardım" tool window (see ToolWindow.cs) — a real separate, non-modal OS window,
 * not an in-page dialog. */
export function HelpWindow() {
  const { t } = useT();
  const [category, setCategory] = useState<Category>("licenses");

  const categories = [
    { id: "licenses", label: t("menu.help.licenses") },
    { id: "agreement", label: t("menu.help.agreement") },
  ];

  return (
    <ToolWindowLayout categories={categories} activeId={category} onSelect={(id) => setCategory(id as Category)}>
      <SectionLabel>{t("menu.help.version", APP_VERSION)}</SectionLabel>
      <p style={{ margin: "10px 0 0", fontSize: 12.5, color: "var(--ms-text-secondary)", lineHeight: 1.5, maxWidth: 420 }}>
        {category === "licenses" ? t("help.licensesNote") : t("help.agreementNote")}
      </p>
    </ToolWindowLayout>
  );
}
