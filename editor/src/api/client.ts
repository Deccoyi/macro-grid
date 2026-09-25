import type { Profile } from "@macro/renderer";
import type {
  ActionInfo,
  AppPreferences,
  ExportProfileResult,
  ImportProfileResult,
  OptionsResult,
  IconPackInfo,
  PairedDeviceInfo,
  PairingQrInfo,
  PluginCatalogInstallResult,
  PluginCatalogResponse,
  PluginInfo,
  PluginInstallResult,
  PluginUninstallResult,
  ProfileSummary,
  RunningWindowInfo,
  SettingField,
  StatusEntry,
  UpdateCheckOutcome,
  UpdateSnapshot,
  VariableInfo,
  VariableSnapshot,
} from "./types";

/** Every editor API call must bypass the HTTP cache — a GET right after a save must never return a
 * stale cached body (the same trap as the editor's own HTML/JS bundle caching, see docs/guides/development.md). */
function req(url: string, init?: RequestInit): Promise<Response> {
  return fetch(url, { ...init, cache: "no-store" });
}

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error((body as { error?: string } | null)?.error ?? `${res.status} ${res.statusText}`);
  }
  // 204 (and any other empty-body success, e.g. Results.Ok() with no payload) has nothing to parse.
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

/** GET a JSON resource. */
function get<T>(url: string): Promise<T> {
  return req(url).then((res) => json<T>(res));
}

/** Send a request with an optional JSON body and read the JSON (or empty) response. */
function send<T>(method: "POST" | "PUT" | "DELETE", url: string, body?: unknown): Promise<T> {
  const init: RequestInit = { method };
  if (body !== undefined) {
    init.headers = { "Content-Type": "application/json" };
    init.body = JSON.stringify(body);
  }
  return req(url, init).then((res) => json<T>(res));
}

const pluginPath = (id: string) => `/api/plugins/${encodeURIComponent(id)}`;

/** The legal texts that ship with the app (GET /api/legal); a text is null when its file is missing, as in a development build. */
export interface LegalOverview {
  agreement: string | null;
  projectLicense: string | null;
  notices: string | null;
  libraries: string[];
}

export const api = {
  listProfiles: (): Promise<ProfileSummary[]> => get("/api/profiles"),

  getProfile: (id: string): Promise<Profile> => get(`/api/profiles/${id}`),

  createProfile: (): Promise<Profile> => send("POST", "/api/profiles"),

  saveProfile: (profile: Profile): Promise<void> => send("PUT", `/api/profiles/${profile.id}`, profile),

  deleteProfile: (id: string): Promise<void> => send("DELETE", `/api/profiles/${id}`),

  listActions: (): Promise<ActionInfo[]> => get("/api/actions"),

  /** Dynamic dropdown options for an action's field (e.g. a scene or audio-input list) — `currentValues`
   * is the current form state for the field's `dependsOn` keys. */
  getActionOptions: (type: string, sourceId: string, currentValues: Record<string, unknown>): Promise<OptionsResult> =>
    send("POST", `/api/actions/${encodeURIComponent(type)}/options/${encodeURIComponent(sourceId)}`, currentValues),

  /** The running server's version, the one place it is defined (ClientHub.ServerVersion). */
  getVersion: (): Promise<{ version: string }> => get("/api/version"),

  getStatus: (): Promise<StatusEntry[]> => get("/api/status"),

  variablesSnapshot: (): Promise<VariableSnapshot> => get("/api/variables/snapshot"),

  variableCatalog: (): Promise<VariableInfo[]> => get("/api/variables/catalog"),

  /** Shows a native "choose an .exe" dialog on the server's desktop and returns the chosen path, or null if canceled. */
  browseForExecutable: (): Promise<string | null> =>
    send<{ path: string | null }>("POST", "/api/browse/executable").then((r) => r.path),

  /** Shows a native "Open" dialog for a SettingFieldKind.File field and returns only the chosen path (the
   * plugin stores the path itself and reads the file on its own — nothing is uploaded). */
  browseForFile: (title: string, filter: string): Promise<string | null> =>
    send<{ path: string | null }>("POST", "/api/browse/file", { title, filter }).then((r) => r.path),

  pairingQr: (): Promise<PairingQrInfo> => get("/api/pairing/qr"),

  regeneratePairingQr: (): Promise<PairingQrInfo> => send("POST", "/api/pairing/qr/regenerate"),

  listDevices: (): Promise<PairedDeviceInfo[]> => get("/api/devices"),

  revokeDevice: (id: string): Promise<void> => send("DELETE", `/api/devices/${id}`),

  assignDeviceProfile: (id: string, profileId: string | null): Promise<void> =>
    send("PUT", `/api/devices/${id}/profile`, { profileId }),

  setDeviceFollowActiveWindow: (id: string, followActiveWindow: boolean): Promise<void> =>
    send("PUT", `/api/devices/${id}/follow-window`, { followActiveWindow }),

  listRunningWindows: (): Promise<RunningWindowInfo[]> => get("/api/system/windows"),

  listPlugins: (): Promise<PluginInfo[]> => get("/api/plugins"),

  listIconPacks: (): Promise<IconPackInfo[]> => get("/api/icon-packs"),

  /** Raw SVG markup (not JSON) for one icon in a plugin-contributed pack. */
  getIconPackIconSvg: (packId: string, iconName: string): Promise<string> =>
    req(`/api/icon-packs/${encodeURIComponent(packId)}/${encodeURIComponent(iconName)}`).then((res) => {
      if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
      return res.text();
    }),

  /** Shows a native "choose a folder" dialog on the server's desktop, validates plugin.json there, copies it
   * into the server's plugins/ folder and loads it right away (no restart). Installing over an existing id
   * replaces that plugin. */
  installPluginDialog: (): Promise<PluginInstallResult> => send("POST", "/api/plugins/install"),

  /** Unloads a plugin (its actions, variables and status items disappear immediately) and deletes its
   * folder under %AppData%. `pending` means a file was still in use and the folder goes at the next start. */
  uninstallPlugin: (id: string): Promise<PluginUninstallResult> => send("DELETE", pluginPath(id)),

  /** Approves the permissions a JS plugin declares and starts it. */
  approvePlugin: (id: string): Promise<PluginInfo> => send("POST", `${pluginPath(id)}/approve`),

  /** Unloads a plugin and loads it again from its folder, picking up a replaced DLL or changed manifest. */
  reloadPlugin: (id: string): Promise<PluginInfo> => send("POST", `${pluginPath(id)}/reload`),

  /** A registered IPluginSettingsPage's form schema — 404 if the plugin has none (PluginInfo.hasSettings
   * is false), in which case the editor has no generic fallback UI for that plugin's settings anymore. */
  getPluginSettingsSchema: (id: string): Promise<SettingField[]> => get(`${pluginPath(id)}/settings/schema`),

  getPluginSettings: (id: string): Promise<Record<string, unknown>> => get(`${pluginPath(id)}/settings`),

  savePluginSettings: (id: string, values: Record<string, unknown>): Promise<void> =>
    send("PUT", `${pluginPath(id)}/settings`, values),

  getPluginSettingsOptions: (id: string, sourceId: string, currentValues: Record<string, unknown>): Promise<OptionsResult> =>
    send("POST", `${pluginPath(id)}/settings/options/${encodeURIComponent(sourceId)}`, currentValues),

  /** Runs a SettingFieldKind.Button field's command against the plugin's ISettingsCommandHandler (e.g. a
   * sound preview) — `values` is the current form/row values. 404 (thrown as an Error) if the plugin's
   * settings page does not implement that interface. The returned text is a short info/error message. */
  runPluginSettingsCommand: (id: string, command: string, values: Record<string, unknown>): Promise<{ text: string | null }> =>
    send("POST", `${pluginPath(id)}/settings/command`, { command, values }),

  /** URL of a plugin's manifest logo (PluginInfo.hasIcon) — an <img src>, not a fetch: nothing to parse. */
  getPluginIconUrl: (id: string): string => `${pluginPath(id)}/icon`,

  /** Discover tab: browses a source's plugins (today, only `"official"`) — fetched fresh every time the tab
   * opens or is refreshed, never in the background. */
  fetchPluginCatalog: (source: string): Promise<PluginCatalogResponse> =>
    get(`/api/plugin-catalog?source=${encodeURIComponent(source)}`),

  /** Downloads, verifies and installs one version from a catalog source through the same pipeline as a local
   * folder install (PluginManager.InstallFromFolderAsync), plus hash/signature checks first. */
  installFromPluginCatalog: (source: string, id: string, version: string): Promise<PluginCatalogInstallResult> =>
    send("POST", "/api/plugin-catalog/install", { source, id, version }),

  getPreferences: (): Promise<AppPreferences> => get("/api/preferences"),

  /** Whether Macro Grid starts when the person signs in to Windows (the per-user Run entry the installer's task also writes). */
  getAutostart: (): Promise<{ enabled: boolean }> => get("/api/system/autostart"),

  setAutostart: (enabled: boolean): Promise<{ enabled: boolean }> => send("PUT", "/api/system/autostart", { enabled }),

  savePreferences: (preferences: AppPreferences): Promise<void> => send("PUT", "/api/preferences", preferences),

  /** Shows a native "Open" dialog on the server's desktop and reads the chosen .msprofile (or plain profile JSON)
   * file; the server validates it and reports which plugins it needs that are missing. */
  importProfileDialog: (): Promise<ImportProfileResult> => send("POST", "/api/browse/import-profile"),

  /** Shows a native "Save As" dialog on the server's desktop and writes the profile there as a .msprofile
   * package (the profile plus a manifest naming the plugins it needs). */
  exportProfileDialog: (profile: Profile): Promise<ExportProfileResult> => send("POST", "/api/browse/export-profile", profile),

  /** Opens (or focuses) a real, separate OS window for a tool panel — see docs/ui/ui-guidelines.md:
   * Preferences/Plugins are native windows, not in-page modals. */
  openToolWindow: (kind: "preferences" | "plugins" | "help" | "pairing" | "update", tab?: string): Promise<void> =>
    send("POST", `/api/windows/${kind}${tab ? `?tab=${encodeURIComponent(tab)}` : ""}`),

  /** The agreement, the project license, the third-party notice index and the names of the bundled libraries (null when a file is missing, as in a development build). */
  getLegal: (): Promise<LegalOverview> => get("/api/legal"),

  /** The original license text of one bundled library. */
  getLegalLibrary: (name: string): Promise<string> =>
    req(`/api/legal/library/${encodeURIComponent(name)}`).then((res) => res.text()),

  /** The server's update state: the running version and the release that is available, if any. */
  getUpdate: (): Promise<UpdateSnapshot> => get("/api/update"),

  /** A check the person asked for; it ignores "Later" and "Skip this version". */
  checkForUpdates: (): Promise<{ outcome: UpdateCheckOutcome; snapshot: UpdateSnapshot }> => send("POST", "/api/update/check"),

  /** "Install now": starts the download in the background; follow it in getUpdate().install. */
  installUpdate: (): Promise<void> => send("POST", "/api/update/install"),

  /** "Later": no notification for 24 hours. */
  snoozeUpdate: (): Promise<void> => send("POST", "/api/update/snooze"),

  /** "Skip this version": no notification for the offered version again. */
  skipUpdate: (): Promise<void> => send("POST", "/api/update/skip"),

  openPluginSettingsWindow: (id: string): Promise<void> =>
    send("POST", `/api/windows/plugin-settings/${encodeURIComponent(id)}`),
};
