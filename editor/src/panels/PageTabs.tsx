import { X } from "lucide-react";
import type { Page } from "@macro/renderer";

export interface PageTabsProps {
  pages: Page[];
  currentPageId: string | null;
  onSelect: (pageId: string) => void;
  onAdd: () => void;
  onRename: (pageId: string, name: string) => void;
  onDelete: (pageId: string) => void;
}

export function PageTabs({ pages, currentPageId, onSelect, onAdd, onRename, onDelete }: PageTabsProps) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 4, padding: "6px 10px", borderBottom: "1px solid var(--ms-border)" }}>
      {pages.map((page) => (
        <div key={page.id} style={{ display: "flex", alignItems: "center" }}>
          <button
            className={page.id === currentPageId ? "active" : "ghost"}
            onClick={() => onSelect(page.id)}
            onDoubleClick={() => {
              const name = prompt("Sayfa adı", page.name);
              if (name) onRename(page.id, name);
            }}
          >
            {page.name}
          </button>
          {pages.length > 1 && page.id === currentPageId && (
            <button
              className="ghost"
              title="Sayfayı sil"
              onClick={() => {
                if (confirm(`"${page.name}" sayfası silinsin mi?`)) onDelete(page.id);
              }}
              style={{ padding: "5px 7px", color: "var(--ms-text-secondary)", display: "inline-flex" }}
            >
              <X size={13} />
            </button>
          )}
        </div>
      ))}
      <button className="ghost" onClick={onAdd} title="Yeni sayfa">
        + Sayfa
      </button>
    </div>
  );
}
