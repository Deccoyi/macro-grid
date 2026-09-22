import type { Profile } from "@macro/renderer";
import type { ActionInfo, PairedDeviceInfo, ProfileSummary, VariableInfo, VariableSnapshot } from "./types";

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

  pairingPin: (): Promise<string> => req("/api/pairing/pin").then((res) => json<{ pin: string }>(res)).then((r) => r.pin),

  regeneratePairingPin: (): Promise<string> =>
    req("/api/pairing/pin/regenerate", { method: "POST" }).then((res) => json<{ pin: string }>(res)).then((r) => r.pin),

  listDevices: (): Promise<PairedDeviceInfo[]> => req("/api/devices").then((res) => json<PairedDeviceInfo[]>(res)),

  revokeDevice: (id: string): Promise<void> => req(`/api/devices/${id}`, { method: "DELETE" }).then((res) => json<void>(res)),
};
