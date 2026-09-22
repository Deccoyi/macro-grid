import { useState } from "react";
import { useT } from "../i18n/I18nContext";
import { SectionLabel } from "../panels/fields/controls";
import { ToolWindowLayout } from "./ToolWindowLayout";

type Category = "installed" | "discover";

/** The whole page of the "Eklentiler" tool window (see ToolWindow.cs) — a real separate, non-modal OS
 * window. The plugin loader itself is a separate, later development phase; this is the shell it will
 * plug into. */
export function PluginsWindow() {
  const { t } = useT();
  const [category, setCategory] = useState<Category>("installed");

  const categories = [
    { id: "installed", label: t("plugins.category.installed") },
    { id: "discover", label: t("plugins.category.discover") },
  ];

  return (
    <ToolWindowLayout categories={categories} activeId={category} onSelect={(id) => setCategory(id as Category)}>
      <SectionLabel>{categories.find((c) => c.id === category)!.label}</SectionLabel>
      <p style={{ margin: "10px 0 0", fontSize: 12.5, color: "var(--ms-text-secondary)", lineHeight: 1.5, maxWidth: 420 }}>
        {t("plugins.comingSoon.body")}
      </p>
    </ToolWindowLayout>
  );
}
