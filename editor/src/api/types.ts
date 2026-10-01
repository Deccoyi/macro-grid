import type { ActionBinding, ConditionNode, Profile } from "@macro/renderer";
type SettingFieldKind = "Text" | "Password" | "Number" | "Slider" | "Bool" | "Select" | "Segmented" | "File" | "List" | "Button" | "Notice" | "Variable" | "Color";

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

/** One node of the root profile tree — docs/design/hierarchy-tree-and-folders.md, mirrors
 * MacroGrid.Core.Profiles.ProfileTreeNode. Same shape convention as the renderer's PageTreeNode: a "type"
 * discriminator instead of subtypes, a "profile" node only ever setting `id`. */
export interface ProfileTreeNode {
  type: "profile" | "folder";
  id: string;
  name?: string;
  children?: ProfileTreeNode[];
}

export type VariableSnapshot = Record<string, unknown>;

/** Mirrors MacroGrid.Plugin.Abstractions.VariableType (sent as these strings). */
export type VariableType = "text" | "number" | "boolean" | "duration" | "dateTime";

/** Mirrors MacroGrid.Core.Variables.UserVariable: a variable from the Global Variable List. The name and type never change after creation. */
export type UserVariableType = "text" | "number" | "boolean";

export interface UserVariableDef {
  name: string;
  type: UserVariableType;
  /** The start value; missing or null means "no value yet" (a text variable always has one). */
  initial?: string | number | boolean | null;
  keep: boolean;
  description: string;
}

export interface UserVariablesResponse {
  variables: UserVariableDef[];
  limits: { maxCount: number; maxNameLength: number; maxDescriptionLength: number; maxTextLength: number };
}

/** Mirrors MacroGrid.Core.Automation: one trigger; only the fields of its kind are used. */
export type AutomationTriggerKind = "variable" | "time" | "deviceConnect";

export interface AutomationTrigger {
  kind: AutomationTriggerKind;
  condition?: ConditionNode | null;
  /** "HH:mm". */
  time?: string | null;
  /** 0 = Sunday ... 6 = Saturday; empty = every day. */
  days: number[];
  /** null = any paired device. */
  deviceId?: string | null;
}

export interface AutomationRuleDef {
  id: string;
  name: string;
  enabled: boolean;
  trigger: AutomationTrigger;
  actions: ActionBinding[];
  cooldownSeconds: number;
}

export interface AutomationRuleStatus {
  running: boolean;
  lastStart?: string | null;
  lastResult: "none" | "ok" | "failed" | "refused";
  lastMessage?: string | null;
  runs: number;
}

export interface AutomationResponse {
  paused: boolean;
  rules: AutomationRuleDef[];
  limits: { maxRules: number; maxNameLength: number; maxSteps: number; maxCooldownSeconds: number; maxComparisons: number };
  status: Record<string, AutomationRuleStatus>;
}

export interface AutomationRuleUse {
  id: string;
  name: string;
}

export interface UserVariableUse {
  profileId: string;
  profileName: string;
  pageId: string;
  pageName: string;
  widgetId: string;
  widgetName: string;
  spot: "text" | "dynamic" | "action" | "props";
}

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
  status: "Loaded" | "Incompatible" | "Error" | "NeedsApproval" | "NotAllowed";
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
  /** The person chose to stay on this version (no update offered). Missing from an older server. */
  held?: boolean;
  /** True when the running plugin implements the optional `IPluginTreeProvider` — only then does the Plugins
   * tool window give it a chevron and ask GET /api/plugins/{id}/tree-items for anything. Missing from an
   * older server (treat as false). See docs/design/plugins-tool-window.md. */
  hasTreeItems?: boolean;
  /** True only in a development build, for a C# plugin that loaded without a valid signature. */
  unsigned?: boolean;
  /** The installed version was withdrawn by its publisher (from the saved catalog copy; no network). Missing from an older server. */
  withdrawn?: boolean;
  /** How many button presses used the keyboard through this JavaScript plugin today. Missing from an older server. */
  keyboardUsesToday?: number;
  /** A running JavaScript plugin's approved permissions, and the ones the person switched off. Missing from an older server. */
  permissions?: string[] | null;
  switchedOffPermissions?: string[] | null;
}

/** Mirrors MacroGrid.Plugin.Abstractions.PluginTreeItem — one node of a plugin's own tree in the Plugins tool
 * window (docs/design/plugins-tool-window.md). `icon` is a lucide-react name (the same names a status item
 * uses); an unknown or missing name falls back to a plain dot. */
export interface PluginTreeItem {
  id: string;
  label: string;
  icon?: string | null;
  hasChildren?: boolean;
  tooltip?: string | null;
  hasSettings?: boolean;
}

/** GET /api/plugins/{id}/tree-items — one page of a tree level. */
export interface PluginTreeItemsResult {
  items: PluginTreeItem[];
  continuationToken?: string | null;
}

/** One entry of GET /api/plugins/tree-changes — mirrors MacroGrid.Core.Plugins.PluginTreeChange. `parentId`
 * null means the plugin's top level; `wholePlugin` means the plugin itself was loaded, reloaded or removed. */
export interface PluginTreeChange {
  revision: number;
  pluginId: string;
  parentId?: string | null;
  wholePlugin: boolean;
}

/** GET /api/plugins/tree-changes?since=N — mirrors MacroGrid.Core.Plugins.PluginTreeChanges. `reset` means
 * `since` is older than what the server still remembers: the editor must drop its whole tree-item cache. */
export interface PluginTreeChanges {
  revision: number;
  changes: PluginTreeChange[];
  reset: boolean;
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

/** POST /api/plugins/install/browse — the folder is only read, not installed yet, so the editor can warn
 * about a native (C#) plugin's full trust (see docs/plans/security-hardening-plan.md, part D) before the
 * person confirms with POST /api/plugins/install/confirm. */
export interface PluginInstallBrowseResult {
  canceled: boolean;
  path?: string;
  id?: string;
  name?: string;
  kind?: string;
  permissions?: string[];
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
  /** What to browse by and search words, from the catalog; missing or empty when the plugin did not declare any. */
  category?: string | null;
  tags?: string[];
  /** True when the catalog names an icon for this plugin: fetch it from api.pluginCatalogIconUrl(source, id). */
  hasIcon?: boolean;
  /** The newest version listed, whether or not this server can run it. */
  latestVersion: string | null;
  /** The newest version this server's SDK and version actually satisfy — what Install would fetch. Null
   * when nothing in the catalog is compatible. */
  installableVersion: string | null;
  compatible: boolean;
  /** Why the latest version cannot be installed here (for example "Needs Macro Grid editor 1.3.0 or newer, this is 1.2.4"); null when compatible. */
  incompatibleReason: string | null;
  /** True when the entry lists versions but the publisher withdrew every one (or the official list switched them off): nothing is left to install. Missing from an older server. */
  withdrawn?: boolean;
  /** The installed version carries the publisher's withdrawn flag. Missing from an older server. */
  installedWithdrawn?: boolean;
  permissions: string[];
  installed: boolean;
  installedVersion: string | null;
  /** True when installableVersion is newer than the installed version, the plugin came from this source, and the person did not choose to stay on the installed version (unless it was withdrawn or switched off). */
  updateAvailable: boolean;
  /** False when the plugin was installed from a different source than the one listed. Missing from an older server (treat as true). */
  sameSource?: boolean;
  /** The person chose to stay on the installed version. Missing from an older server. */
  held?: boolean;
  /** The next older version on offer below the installed one, with the permissions it asks for; null when there is none. */
  previous?: { version: string; permissions: string[] } | null;
  /** The source the plugin was installed from, when that is not the listed one. */
  otherSource?: string | null;
  /** "Official" | "ThirdParty" | "Local" | null (not installed and no recorded origin). */
  trust: string | null;
}

/** One line of the Error List that the server reports (GET /api/problems): the same message from the same source is one line with a count. */
export interface ServerProblem {
  id: string;
  /** A plugin id. */
  source: string;
  /** The plugin's display name. */
  sourceName: string;
  severity: "error" | "warning" | "info";
  code: string;
  message: string;
  count: number;
  firstAt: string;
  lastAt: string;
  /** Where in a profile the problem happened, when it belongs to a widget. */
  target?: { profileId: string; pageId?: string; widgetId?: string; event?: string; actionIndex?: number } | null;
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
  /** The oldest Macro Grid the plugin runs on. Older plugins list it as macroGrid, or list sdkVersion and minServerVersion instead. */
  minMacroGrid?: string | null;
  /** Legacy: the earlier name of minMacroGrid, read only when minMacroGrid is absent. */
  macroGrid?: string | null;
  sdkVersion?: string | null;
  minServerVersion?: string | null;
  permissions?: string[];
  compatible?: boolean;
  incompatibleReason?: string | null;
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

/** One named docking-workspace arrangement saved from the View menu. `layoutJson` is the editor's own
 * opaque envelope (dockview's serialized layout plus which tool windows are open/auto-hidden) — the
 * server never looks inside it. */
export interface DockLayoutProfile {
  id: string;
  name: string;
  layoutJson: string;
}

/** Editor-wide preferences, persisted server-side, not in localStorage, so they survive a cleared browser
 * cache or a different WebView profile. */
export interface AppPreferences {
  theme: "dark" | "light";
  /** "tr" or "en", or the tag of an installed language pack. */
  language: string;
  previewProfiles: PreviewProfileInfo[];
  collapsedInspectorSections: Record<string, boolean>;
  /** Notes the person closed with "do not show again", by id. */
  dismissedNotices: Record<string, boolean>;
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
  /** Whether other devices may connect without encryption (the plain port, which the browser deck needs). On by default. */
  allowUnencrypted: boolean;
  /** The docking workspace arrangement last left in — see DockLayoutProfile's `layoutJson`. Empty until
   * the user changes the layout at least once. */
  dockLayoutJson: string;
  /** Named layout snapshots saved from the View menu. The built-in "Default" layout is not one of these. */
  dockLayoutProfiles: DockLayoutProfile[];
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

/** A plugin a packaged profile needs (from the manifest of a .mgprofile file). */
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
  /** Files named by an action that are not on this PC. */
  missingFiles?: { page: string; widget: string; path: string }[];
}

export type RestoreItemKind = "profile" | "profileTree" | "preferences" | "variables" | "automation" | "device" | "pluginSettings" | "languagePack";
export type RestoreItemState = "new" | "different" | "same";

export interface RestoreItem {
  kind: RestoreItemKind;
  key: string;
  name: string;
  state: RestoreItemState;
  names: string[];
  herePages?: number | null;
  hereWidgets?: number | null;
  backupPages?: number | null;
  backupWidgets?: number | null;
  added?: number | null;
  changed?: number | null;
  skipped?: number | null;
  hereVersion?: number | null;
  backupVersion?: number | null;
}

/** A warning is a code plus values; the editor words it (restore.warn.<code>). */
export interface RestoreWarning {
  code: string;
  args: string[];
}

export interface InspectBackupResult {
  path: string | null;
  result?: {
    id: string;
    manifest: { createdAt: string; serverVersion: string; reason: string };
    items: RestoreItem[];
    warnings: RestoreWarning[];
  };
}

export interface RestoreItemResult {
  kind: RestoreItemKind;
  key: string;
  ok: boolean;
  error: string | null;
  warnings: RestoreWarning[];
}

export interface ExportProfileResult {
  path: string | null;
}

/** One custom widget a plugin offers (GET /api/plugin-widgets): what the Toolbox lists and the Inspector draws a settings form from. */
export interface PluginWidgetInfo {
  plugin: string;
  pluginName: string;
  widget: string;
  name: string;
  description?: string | null;
  category?: string | null;
  size: { w: number; h: number };
  fps: number;
  interactive: boolean;
  /** The abilities the widget declared and the person approved (keepLoaded, storage). */
  options: string[];
  /** The declared options that start switched off on a placed widget. */
  optionsOff?: string[];
  /** The widget's Toolbox icon as an SVG data URI, when the plugin ships one. */
  icon?: string | null;
  /** False for a plugin that is not verified (every JavaScript plugin). */
  verified: boolean;
  settings?: SettingField[] | null;
}

/** One installed language pack as GET /api/languages lists it. */
export interface LanguagePackInfo {
  tag: string;
  name: string;
  version: number;
}

/** The stored file of a language pack (docs/design/language-packs.md). The server checks the container; the editor checks every string. */
export interface LanguagePackFile {
  meta: { format: number; tag: string; name: string; version: number; appVersion?: string };
  strings: Record<string, string>;
  /** A short hash of the English text each row was translated from, to find rows that need review. */
  sources?: Record<string, string>;
}
