export interface ActionInfo {
  type: string;
  displayName: string;
}

export interface ProfileSummary {
  id: string;
  name: string;
}

export type VariableSnapshot = Record<string, unknown>;

export interface VariableInfo {
  name: string;
  description: string;
  example: string;
  category: string;
}

/** Mirrors MacroStation.Core.Plugins.LoadedPlugin. `status` is "Loaded" | "Incompatible" | "Error"
 * (C# enum names as sent by System.Text.Json's default Web naming — see PluginLoader.cs). */
export interface PluginInfo {
  id: string;
  name: string;
  version: string;
  status: "Loaded" | "Incompatible" | "Error";
  detail: string | null;
}

/** OBS plugin's own settings.json shape (see macro-station-plugins/OBS/src/ObsSettings.cs) — read/written
 * through the generic `/api/plugins/{id}/settings` passthrough, not a host-side schema. */
export interface ObsPluginSettings {
  enabled: boolean;
  host: string;
  port: number;
  password: string;
}

export interface PluginInstallResult {
  installed: boolean;
  canceled?: boolean;
  id?: string;
  name?: string;
  requiresRestart?: boolean;
}

export interface PairedDeviceInfo {
  id: string;
  name: string;
  pairedAt: string;
  lastSeenAt: string;
  assignedProfileId: string | null;
}

/** Pairing QR payload. `text` is the `macrostation://pair?...` URI to encode — empty if the server
 * has no LAN adapter up (nothing to reach it on), in which case the editor should warn instead of
 * showing a QR code. `pin` is also shown as text for manual entry if the camera scan doesn't work. */
export interface PairingQrInfo {
  text: string;
  host: string;
  port: number;
  pin: string;
  expiresAt: string;
}

export interface PreviewProfileInfo {
  id: string;
  name: string;
  width: number;
  height: number;
}

/** Editor-wide preferences, persisted server-side (see docs/agent-notes.md — not localStorage, so they
 * survive a cleared browser cache or opening the editor from a different machine on the LAN). */
export interface AppPreferences {
  theme: "dark" | "light";
  language: "tr" | "en";
  previewProfiles: PreviewProfileInfo[];
  collapsedInspectorSections: Record<string, boolean>;
}

export interface ImportProfileResult {
  path: string | null;
  content: string | null;
}

export interface ExportProfileResult {
  path: string | null;
}
