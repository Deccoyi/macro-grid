import type { ReactNode } from "react";
import { X } from "lucide-react";
import { useT } from "../../i18n/I18nContext";

interface DzTitleBarProps {
  icon: ReactNode;
  title: string;
  subtitle?: string;
  /** Render the subtitle in the mono face (a property or variable name). */
  mono?: boolean;
  onClose: () => void;
}

/** The title bar both Dynamize windows share: 48px, accent-muted icon tile, title + subtitle, close button. Square corners, no radius (docs/ui/dynamize-window-anatomy.md §3). */
export function DzTitleBar({ icon, title, subtitle, mono, onClose }: DzTitleBarProps) {
  const { t } = useT();
  return (
    <div className="dz-titlebar">
      <div className="tile">{icon}</div>
      <div className="text">
        <div className="title">{title}</div>
        {subtitle && <div className={mono ? "subtitle mono" : "subtitle"}>{subtitle}</div>}
      </div>
      <div className="spacer" />
      <button type="button" className="ghost pf-icon-btn small" onClick={onClose} aria-label={t("header.close")}>
        <X size={14} />
      </button>
    </div>
  );
}
