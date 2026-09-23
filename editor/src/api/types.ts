export type SettingFieldKind = "Text" | "Password" | "Number" | "Slider" | "Bool" | "Select" | "Segmented";

export interface SettingOption {
  value: string;
  label: string;
  group?: string | null;
  icon?: string | null;
}

/** Mirrors MacroStation.Plugin.Abstractions.SettingField — one field of a schema-driven form (action
 * settings or a plugin's own settings page), rendered generically by SchemaForm.tsx. */
export interface SettingField {
  key: string;
  label: string;
  kind: SettingFieldKind;
  description?: string | null;
  placeholder?: string | null;
  default?: unknown;
  min?: number | null;
  max?: number | null;
  step?: number | null;
  options?: SettingOption[] | null;
  optionsSource?: string | null;
  dependsOn?: string[] | null;
  allowVariables?: boolean;
  visibleWhen?: string | null;
}

/** Mirrors MacroStation.Plugin.Abstractions.OptionsResult — the response of a dynamic-dropdown query. */
export interface OptionsResult {
  options: SettingOption[];
  error?: string | null;
}

export interface ActionInfo {
  type: string;
  displayName: string;
  category: string;
  description?: string | null;
  icon?: string | null;
  pluginId?: string | null;
  /** Present (non-empty) only when the action has no hand-written form — SchemaForm renders it. */
  fields?: SettingField[] | null;
}

export type StatusLevel = "Idle" | "Ok" | "Busy" | "Warning" | "Error";

/** One entry in the editor's window-wide status bar (see components/StatusBar.tsx). */
export interface StatusEntry {
  pluginId: string;
  id: string;
  text: string;
  level: StatusLevel;
  icon?: string | null;
  tooltip?: string | null;
  updatedAt: string;
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
  hasSettings: boolean;
}

/** One icon pack contributed by a plugin via IPluginHost.RegisterIconPack — e.g. the PLC icon set.
 * Lucide (bundled locally, see IconPicker.tsx) is not one of these; it's the picker's built-in default. */
export interface IconPackInfo {
  id: string;
  displayName: string;
  icons: string[];
}

export interface PluginInstallResult {
  installed: boolean;
  canceled?: boolean;
  id?: string;
  name?: string;
  requiresRestart?: boolean;
}

export interface PluginUninstallResult {
  removed: boolean;
  /** true if the folder couldn't be deleted outright (plugin still loaded in the running process) and was
   * instead marked for removal on the next server start — see ServerApp.cs's DELETE /api/plugins/{id}. */
  pending: boolean;
  requiresRestart: boolean;
}

export interface PairedDeviceInfo {
  id: string;
  name: string;
  pairedAt: string;
  lastSeenAt: string;
  assignedProfileId: string | null;
  /** Opt-in: this device's session auto-switches profile based on the server's foreground window — see
   * docs/auto-profile-switch.md. */
  followActiveWindow: boolean;
  /** The drawer's auto-switch pause, persisted so it survives a reconnect. */
  autoSwitchLocked: boolean;
}

/** One process currently owning a visible top-level window — GET /api/system/windows, for the "çalışan
 * uygulamadan seç" picker on a profile's auto-switch rules. */
export interface RunningWindowInfo {
  processName: string;
  title: string;
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
  /** Fallback profile a device resolves to with no explicit assignment and no auto-switch rule currently
   * applying — see docs/auto-profile-switch.md. Null means "no preference set". */
  defaultProfileId: string | null;
}

export interface ImportProfileResult {
  path: string | null;
  content: string | null;
}

export interface ExportProfileResult {
  path: string | null;
}
