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
  PluginInfo,
  PluginInstallResult,
  PluginUninstallResult,
  ProfileSummary,
  RunningWindowInfo,
  SettingField,
  StatusEntry,
  VariableInfo,
  VariableSnapshot,
} from "./types";

/** Every editor API call must bypass the HTTP cache — a GET right after a save must never return a
 * stale cached body (see docs/agent-notes.md: same trap as the editor's own HTML/JS bundle caching). */
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

export const api = {
  listProfiles: (): Promise<ProfileSummary[]> => req("/api/profiles").then((res) => json<ProfileSummary[]>(res)),

  getProfile: (id: string): Promise<Profile> => req(`/api/profiles/${id}`).then((res) => json<Profile>(res)),

  createProfile: (): Promise<Profile> => req("/api/profiles", { method: "POST" }).then((res) => json<Profile>(res)),

  saveProfile: (profile: Profile): Promise<void> =>
    req(`/api/profiles/${profile.id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(profile),
    }).then((res) => json<void>(res)),

  deleteProfile: (id: string): Promise<void> => req(`/api/profiles/${id}`, { method: "DELETE" }).then((res) => json<void>(res)),

  listActions: (): Promise<ActionInfo[]> => req("/api/actions").then((res) => json<ActionInfo[]>(res)),

  /** Dynamic dropdown options for an action's field (e.g. OBS's scene/audio-input lists) — `currentValues`
   * is the current form state for the field's `dependsOn` keys. */
  getActionOptions: (type: string, sourceId: string, currentValues: Record<string, unknown>): Promise<OptionsResult> =>
    req(`/api/actions/${encodeURIComponent(type)}/options/${encodeURIComponent(sourceId)}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(currentValues),
    }).then((res) => json<OptionsResult>(res)),

  getStatus: (): Promise<StatusEntry[]> => req("/api/status").then((res) => json<StatusEntry[]>(res)),

  variablesSnapshot: (): Promise<VariableSnapshot> => req("/api/variables/snapshot").then((res) => json<VariableSnapshot>(res)),

  variableCatalog: (): Promise<VariableInfo[]> => req("/api/variables/catalog").then((res) => json<VariableInfo[]>(res)),

  /** Shows a native "choose an .exe" dialog on the server's desktop and returns the chosen path, or null if canceled. */
  browseForExecutable: (): Promise<string | null> =>
    req("/api/browse/executable", { method: "POST" }).then((res) => json<{ path: string | null }>(res)).then((r) => r.path),

  pairingQr: (): Promise<PairingQrInfo> => req("/api/pairing/qr").then((res) => json<PairingQrInfo>(res)),

  regeneratePairingQr: (): Promise<PairingQrInfo> =>
    req("/api/pairing/qr/regenerate", { method: "POST" }).then((res) => json<PairingQrInfo>(res)),

  listDevices: (): Promise<PairedDeviceInfo[]> => req("/api/devices").then((res) => json<PairedDeviceInfo[]>(res)),

  revokeDevice: (id: string): Promise<void> => req(`/api/devices/${id}`, { method: "DELETE" }).then((res) => json<void>(res)),

  assignDeviceProfile: (id: string, profileId: string | null): Promise<void> =>
    req(`/api/devices/${id}/profile`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ profileId }),
    }).then((res) => json<void>(res)),

  setDeviceFollowActiveWindow: (id: string, followActiveWindow: boolean): Promise<void> =>
    req(`/api/devices/${id}/follow-window`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ followActiveWindow }),
    }).then((res) => json<void>(res)),

  listRunningWindows: (): Promise<RunningWindowInfo[]> => req("/api/system/windows").then((res) => json<RunningWindowInfo[]>(res)),

  listPlugins: (): Promise<PluginInfo[]> => req("/api/plugins").then((res) => json<PluginInfo[]>(res)),

  listIconPacks: (): Promise<IconPackInfo[]> => req("/api/icon-packs").then((res) => json<IconPackInfo[]>(res)),

  /** Raw SVG markup (not JSON) for one icon in a plugin-contributed pack. */
  getIconPackIconSvg: (packId: string, iconName: string): Promise<string> =>
    req(`/api/icon-packs/${encodeURIComponent(packId)}/${encodeURIComponent(iconName)}`).then((res) => {
      if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
      return res.text();
    }),

  /** Shows a native "choose a folder" dialog on the server's desktop, validates plugin.json there, copies it
   * into the server's plugins/ folder and loads it right away (no restart). Installing over an existing id
   * replaces that plugin. */
  installPluginDialog: (): Promise<PluginInstallResult> =>
    req("/api/plugins/install", { method: "POST" }).then((res) => json<PluginInstallResult>(res)),

  /** Unloads a plugin (its actions, variables and status items disappear immediately) and deletes its
   * folder under %AppData%. `pending` means a file was still in use and the folder goes at the next start. */
  uninstallPlugin: (id: string): Promise<PluginUninstallResult> =>
    req(`/api/plugins/${encodeURIComponent(id)}`, { method: "DELETE" }).then((res) => json<PluginUninstallResult>(res)),

  /** Unloads a plugin and loads it again from its folder, picking up a replaced DLL or changed manifest. */
  reloadPlugin: (id: string): Promise<PluginInfo> =>
    req(`/api/plugins/${encodeURIComponent(id)}/reload`, { method: "POST" }).then((res) => json<PluginInfo>(res)),

  /** A registered IPluginSettingsPage's form schema — 404 if the plugin has none (PluginInfo.hasSettings
   * is false), in which case the editor has no generic fallback UI for that plugin's settings anymore. */
  getPluginSettingsSchema: (id: string): Promise<SettingField[]> =>
    req(`/api/plugins/${encodeURIComponent(id)}/settings/schema`).then((res) => json<SettingField[]>(res)),

  getPluginSettings: (id: string): Promise<Record<string, unknown>> =>
    req(`/api/plugins/${encodeURIComponent(id)}/settings`).then((res) => json<Record<string, unknown>>(res)),

  savePluginSettings: (id: string, values: Record<string, unknown>): Promise<void> =>
    req(`/api/plugins/${encodeURIComponent(id)}/settings`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(values),
    }).then((res) => json<void>(res)),

  getPluginSettingsOptions: (id: string, sourceId: string, currentValues: Record<string, unknown>): Promise<OptionsResult> =>
    req(`/api/plugins/${encodeURIComponent(id)}/settings/options/${encodeURIComponent(sourceId)}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(currentValues),
    }).then((res) => json<OptionsResult>(res)),

  getPreferences: (): Promise<AppPreferences> => req("/api/preferences").then((res) => json<AppPreferences>(res)),

  savePreferences: (preferences: AppPreferences): Promise<void> =>
    req("/api/preferences", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(preferences),
    }).then((res) => json<void>(res)),

  /** Shows a native "Open" dialog on the server's desktop and reads the chosen JSON file. */
  importProfileDialog: (): Promise<ImportProfileResult> =>
    req("/api/browse/import-profile", { method: "POST" }).then((res) => json<ImportProfileResult>(res)),

  /** Shows a native "Save As" dialog on the server's desktop and writes the profile JSON there. */
  exportProfileDialog: (fileName: string, content: string): Promise<ExportProfileResult> =>
    req("/api/browse/export-profile", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fileName, content }),
    }).then((res) => json<ExportProfileResult>(res)),

  /** Opens (or focuses) a real, separate OS window for a tool panel — see docs/ui-guidelines.md:
   * Preferences/Plugins are native windows, not in-page modals. */
  openToolWindow: (kind: "preferences" | "plugins" | "help" | "pairing"): Promise<void> =>
    req(`/api/windows/${kind}`, { method: "POST" }).then((res) => json<void>(res)),

  openPluginSettingsWindow: (id: string): Promise<void> =>
    req(`/api/windows/plugin-settings/${encodeURIComponent(id)}`, { method: "POST" }).then((res) => json<void>(res)),
};
