import type { Profile } from "@macro/renderer";
import type {
  ActionInfo,
  AppPreferences,
  ExportProfileResult,
  ImportProfileResult,
  PairedDeviceInfo,
  PairingQrInfo,
  ProfileSummary,
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
};
