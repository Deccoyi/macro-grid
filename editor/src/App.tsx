import { useState } from "react";
import { Pencil, Smartphone } from "lucide-react";
import { EditorCanvas } from "./grid/EditorCanvas";
import { Inspector } from "./panels/Inspector";
import { PageTabs } from "./panels/PageTabs";
import { PairingPanel } from "./panels/PairingPanel";
import { WidgetPalette } from "./panels/WidgetPalette";
import { useEditorState } from "./state/useEditorState";

export function App() {
  const state = useEditorState();
  const { profile, currentPage } = state;
  const [pairingOpen, setPairingOpen] = useState(false);

  if (!profile || !currentPage) {
    return <div style={{ padding: 20, color: "var(--ms-text-secondary)" }}>Yükleniyor…</div>;
  }

  return (
    <div style={{ display: "grid", gridTemplateRows: "auto auto 1fr", height: "100%" }}>
      <header style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 12px", borderBottom: "1px solid var(--ms-border)" }}>
        <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, color: "var(--ms-text-secondary)" }}>
          Profil
          <select value={profile.id} onChange={(e) => state.selectProfile(e.target.value)} style={{ width: "auto", minWidth: 140 }}>
            {state.profiles.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
        </label>
        <button
          className="ghost"
          title="Profili yeniden adlandır"
          onClick={() => {
            const name = prompt("Profil adı", profile.name);
            if (name && name.trim()) state.renameProfile(name.trim());
          }}
        >
          <Pencil size={13} />
        </button>
        <button className="ghost" onClick={() => state.createProfile()}>+ Profil</button>
        <button
          className="ghost"
          onClick={() => {
            if (confirm(`"${profile.name}" profili silinsin mi?`)) state.deleteProfile(profile.id);
          }}
          disabled={state.profiles.length <= 1}
        >
          Profili sil
        </button>

        <div style={{ flex: 1 }} />

        <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, color: "var(--ms-text-secondary)" }}>
          Grid
          <input
            type="number" min={1} max={24} value={currentPage.cols} style={{ width: 48 }}
            onChange={(e) => state.setPageGrid(currentPage.id, Number(e.target.value), currentPage.rows)}
          />
          ×
          <input
            type="number" min={1} max={24} value={currentPage.rows} style={{ width: 48 }}
            onChange={(e) => state.setPageGrid(currentPage.id, currentPage.cols, Number(e.target.value))}
          />
        </label>

        <button className="ghost" onClick={state.refreshVariables}>Değişkenleri yenile</button>
        <button className="ghost" onClick={() => setPairingOpen(true)} style={{ display: "flex", alignItems: "center", gap: 5 }}>
          <Smartphone size={13} /> Eşleştirme
        </button>
        <button className="primary" onClick={state.save} disabled={!state.dirty || state.saving}>
          {state.saving ? "Kaydediliyor…" : state.dirty ? "Kaydet" : "Kaydedildi"}
        </button>
      </header>

      {pairingOpen && <PairingPanel onClose={() => setPairingOpen(false)} />}

      {state.error && (
        <div style={{ padding: "6px 12px", background: "rgba(192,57,43,.15)", color: "var(--ms-danger)", fontSize: 12, display: "flex", justifyContent: "space-between" }}>
          <span>{state.error}</span>
          <button className="ghost" onClick={state.clearError}>Kapat</button>
        </div>
      )}

      <PageTabs
        pages={profile.pages}
        currentPageId={state.currentPageId}
        onSelect={state.setCurrentPageId}
        onAdd={state.addPage}
        onRename={state.renamePage}
        onDelete={state.deletePage}
      />

      <div style={{ display: "grid", gridTemplateColumns: "160px 1fr 300px", minHeight: 0 }}>
        <div style={{ borderRight: "1px solid var(--ms-border)", overflowY: "auto" }}>
          <WidgetPalette onAdd={state.addWidget} />
        </div>

        <div style={{ padding: 16, minWidth: 0, minHeight: 0 }}>
          <div style={{ width: "100%", height: "100%", border: "1px solid var(--ms-border)", borderRadius: 4, background: "var(--ms-bg-surface)" }}>
            <EditorCanvas
              page={currentPage}
              selectedWidgetId={state.selectedWidgetId}
              onSelect={state.setSelectedWidgetId}
              onRectChange={state.setWidgetRect}
              variables={state.variables}
            />
          </div>
        </div>

        <div style={{ borderLeft: "1px solid var(--ms-border)", overflowY: "auto" }}>
          <Inspector
            widget={state.selectedWidget}
            pages={profile.pages}
            profiles={state.profiles}
            actions={state.actions}
            variableCatalog={state.variableCatalog}
            onChange={(fn) => state.selectedWidgetId && state.updateWidget(state.selectedWidgetId, fn)}
            onDelete={() => state.selectedWidgetId && state.deleteWidget(state.selectedWidgetId)}
          />
        </div>
      </div>
    </div>
  );
}
