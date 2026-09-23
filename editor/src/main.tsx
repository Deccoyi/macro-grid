import { createRoot } from "react-dom/client";
import { App } from "./App";
import { PreferencesProvider } from "./preferences/PreferencesContext";
import "./theme.css";
import { HelpWindow } from "./windows/HelpWindow";
import { PairingWindow } from "./windows/PairingWindow";
import { PluginsWindow } from "./windows/PluginsWindow";
import { PreferencesWindow } from "./windows/PreferencesWindow";

// `?window=preferences|plugins|help|pairing` is how ToolWindow.cs points a separate native OS window at
// just that panel's content, full-page — no editor chrome, no modal backdrop (see docs/ui-guidelines.md).
const windowKind = new URLSearchParams(location.search).get("window");

function Root() {
  if (windowKind === "preferences") return <PreferencesWindow />;
  if (windowKind === "plugins") return <PluginsWindow />;
  if (windowKind === "help") return <HelpWindow />;
  if (windowKind === "pairing") return <PairingWindow />;
  return <App />;
}

createRoot(document.getElementById("root")!).render(
  <PreferencesProvider>
    <Root />
  </PreferencesProvider>,
);
