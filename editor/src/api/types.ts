import type { Profile } from "@macro/renderer";
type SettingFieldKind = "Text" | "Password" | "Number" | "Slider" | "Bool" | "Select" | "Segmented" | "File" | "List" | "Button" | "Notice";

export interface SettingOption {
  value: string;
  label: string;
  group?: string | null;
  icon?: string | null;
}

/** Mirrors MacroGrid.Plugin.Abstractions.SettingField — one field of a schema-driven form (action
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
  /** Required for kind "File": a WinForms file filter, e.g. "Audio files (*.wav;*.mp3)|*.wav;*.mp3". */
  fileFilter?: string | null;
  /** Required for kind "List": the schema of one row. Extra row keys outside this schema (a plugin-assigned
   * id, a computed flag, ...) are kept as-is across an edit — never dropped by the form. */
  itemFields?: SettingField[] | null;
  /** Required for kind "Button": the command id sent to the plugin's ISettingsCommandHandler. */
  command?: string | null;
}

/** Mirrors MacroGrid.Plugin.Abstractions.OptionsResult — the response of a dynamic-dropdown query. */
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

/** Mirrors MacroGrid.Plugin.Abstractions.VariableType (sent as these strings). */
export type VariableType = "text" | "number" | "boolean" | "duration" | "dateTime";

export interface VariableInfo {
  name: string;
  description: string;
  example: string;
  category: string;
  /** Missing from an older server; treat as "text". */
  type?: VariableType;
  /** Unit of a number, e.g. "%" or "GB". */
  unit?: string | null;
  /** Allowed values of a fixed-choice text variable. */
  values?: string[] | null;
}

/** Mirrors MacroGrid.Core.Plugins.LoadedPlugin. `status` is "Loaded" | "Incompatible" | "Error"
 * (C# enum names as sent by System.Text.Json's default Web naming — see PluginLoader.cs). */
export interface PluginInfo {
  id: string;
  name: string;
  version: string;
  status: "Loaded" | "Incompatible" | "Error" | "NeedsApproval";
  detail: string | null;
  hasSettings: boolean;
  /** For "NeedsApproval": the permissions a JS plugin declares and is waiting to be allowed. */
  pendingPermissions?: string[] | null;
  /** True when the manifest's optional `icon` path resolved to a valid file — fetch it from
   * api.getPluginIconUrl(id) instead of the generic category glyph. Missing from an older server. */
  hasIcon?: boolean;
  /** "Official" | "ThirdParty" | "Local" — where GET /api/plugins says this plugin came from. Missing from an
   * older server (treat as "Local"). Only GET /api/plugins sends this; approve/reload's single-plugin response
   * does not, since the caller already has it from the list. */
  trust?: "Official" | "ThirdParty" | "Local";
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
  /** How the plugin came up right after being copied in: "Loaded" means it is already live. */
  status?: PluginInfo["status"];
  detail?: string | null;
}

export interface PluginUninstallResult {
  removed: boolean;
  /** true if a file was still in use and the folder was instead marked for removal on the next server
   * start — see ServerApp.cs's DELETE /api/plugins/{id}. The plugin itself is already unloaded either way. */
  pending: boolean;
}

/** One plugin in a catalog response (GET /api/plugin-catalog) — mirrors PluginCatalogApi.DescribeEntry. */
export interface PluginCatalogEntryInfo {
  id: string;
  name: string;
  description: string | null;
  author: string | null;
  homepage: string | null;
  kind: string;
  /** The newest version listed, whether or not this server can run it. */
  latestVersion: string | null;
  /** The newest version this server's SDK and version actually satisfy — what Install would fetch. Null
   * when nothing in the catalog is compatible. */
  installableVersion: string | null;
  compatible: boolean;
  permissions: string[];
  installed: boolean;
  installedVersion: string | null;
  /** True when installableVersion is newer than the installed version. */
  updateAvailable: boolean;
  /** "Official" | "ThirdParty" | "Local" | null (not installed and no recorded origin). */
  trust: string | null;
}

export interface PluginCatalogResponse {
  source: string;
  name: string;
  /** False for an added third-party source — drives the confirmation dialog before installing anything from it. */
  official?: boolean;
  plugins: PluginCatalogEntryInfo[];
  /** Set instead of `plugins` when the source could not be fetched or parsed (offline, malformed index, ...). */
  error?: string;
  code?: string;
}

/** One saved third-party source (GET /api/plugin-sources). */
export interface PluginSourceInfo {
  id: string;
  owner: string;
  repo: string;
  name: string;
  addedAt: string;
}

export interface PluginSourcesResponse {
  official: { id: string; owner: string; repo: string };
  added: PluginSourceInfo[];
}

export interface PluginAddSourceResult extends Partial<PluginSourceInfo> {
  error?: string;
  code?: string;
}

/** POST /api/plugin-link/inspect — a pasted single-plugin repository link, read before anything is downloaded. */
export interface PluginLinkInspectResult {
  /** True when the repo has a macrogrid-index.json instead of a root plugin.json — install it as a source (method 3) instead. */
  isMultiPlugin: boolean;
  owner: string;
  repo: string;
  id?: string;
  name?: string;
  description?: string | null;
  author?: string | null;
  homepage?: string | null;
  kind?: string;
  version?: string;
  sdkVersion?: string;
  minServerVersion?: string;
  permissions?: string[];
  compatible?: boolean;
  error?: string;
  code?: string;
}

export interface PluginLinkInstallResult {
  installed: boolean;
  id?: string;
  name?: string;
  status?: PluginInfo["status"];
  detail?: string | null;
  error?: string;
  code?: string;
}

export interface PluginCatalogInstallResult {
  installed: boolean;
  id?: string;
  name?: string;
  status?: PluginInfo["status"];
  detail?: string | null;
  error?: string;
  code?: string;
}

export interface PairedDeviceInfo {
  id: string;
  name: string;
  pairedAt: string;
  lastSeenAt: string;
  assignedProfileId: string | null;
  /** Opt-in: this device's session auto-switches profile based on the server's foreground window — see
   * docs/design/auto-profile-switch.md. */
  followActiveWindow: boolean;
  /** The drawer's auto-switch pause, persisted so it survives a reconnect. */
  autoSwitchLocked: boolean;
}

/** One process currently owning a visible top-level window — GET /api/system/windows, for the "pick from
 * running apps" picker on a profile's auto-switch rules. */
export interface RunningWindowInfo {
  processName: string;
  title: string;
}

/** Pairing QR payload. `text` is the `macrogrid://pair?...` URI to encode — empty if the server
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

/** Editor-wide preferences, persisted server-side, not in localStorage, so they survive a cleared browser
 * cache or a different WebView profile. */
export interface AppPreferences {
  theme: "dark" | "light";
  language: "tr" | "en";
  previewProfiles: PreviewProfileInfo[];
  collapsedInspectorSections: Record<string, boolean>;
  /** Fallback profile a device resolves to with no explicit assignment and no auto-switch rule currently
   * applying — see docs/design/auto-profile-switch.md. Null means "no preference set". */
  defaultProfileId: string | null;
  /** What happens when the person opens Macro Grid themselves: the editor window ("window", default) or only the tray icon ("tray"). */
  launchMode: "window" | "tray";
  /** What happens when Windows starts Macro Grid at sign-in: only the tray icon ("tray", default) or also the editor window ("window"). */
  autostartMode: "window" | "tray";
  /** Whether the server looks for a newer release by itself (a check shortly after start, then every few hours). */
  checkForUpdates: boolean;
  /** Whether pre-releases (alpha versions) count as updates. */
  includePreReleases: boolean;
}

/** GET /api/update: the running version and, when a newer release exists, what it brings. */
export interface UpdateSnapshot {
  currentVersion: string;
  lastCheckedAt?: string | null;
  checking: boolean;
  error?: string | null;
  install?: UpdateInstallStatus | null;
  available?: {
    version: string;
    name: string;
    pageUrl?: string | null;
    /** Whether "Install now" works for this release (installer and digest are present). */
    canInstall: boolean;
    /** The person chose "Skip this version" for it. */
    skipped: boolean;
    /** Every release between the running version and this one, newest first. */
    releases: { version: string; name: string; notes: string; publishedAt?: string | null }[];
  } | null;
}

/** The state of "Install now" inside GET /api/update. */
export interface UpdateInstallStatus {
  state: "idle" | "downloading" | "starting" | "cancelled" | "failed";
  /** Download progress, 0 to 100. */
  percent: number;
  /** For "failed": declined (administrator prompt refused), refused, download, verify or start. */
  error?: "declined" | "refused" | "download" | "verify" | "start" | null;
}

export type UpdateCheckOutcome = "available" | "upToDate" | "failed";

/** A plugin a packaged profile needs (from the manifest of a .msprofile file). */
interface PackagePluginRef {
  id: string;
  name: string;
  version: string;
  actionTypes: string[];
}

export interface ImportProfileResult {
  /** Null when the user canceled the dialog. */
  path: string | null;
  profile?: Profile;
  /** Plugins the file says it needs that are not installed and loaded on this server. */
  missingPlugins?: PackagePluginRef[];
  /** Action types in the profile that no installed plugin or built-in provides (also covers plain JSON files, which carry no manifest). */
  unknownActionTypes?: string[];
}

export interface ExportProfileResult {
  path: string | null;
}
