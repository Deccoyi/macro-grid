const KEY = "macro-grid.webDeviceId";

/** A stable per-browser-profile id, same role as the Android client's deviceId — lets the server pair
 * this browser once and recognize it across reconnects/tab reloads. Generated once, kept in localStorage
 * (so a different browser, or this one in private mode, is a "new" device that needs its own PIN). */
export function getDeviceId(): string {
  let id = localStorage.getItem(KEY);
  if (!id) {
    id = newId();
    localStorage.setItem(KEY, id);
  }
  return id;
}

/** crypto.randomUUID exists only in a secure context (https or localhost); the deck is normally opened over plain http
 * on the local network, where it is undefined. getRandomValues works everywhere. */
export function newId(): string {
  if (typeof crypto.randomUUID === "function") return crypto.randomUUID();
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6]! & 0x0f) | 0x40;
  bytes[8] = (bytes[8]! & 0x3f) | 0x80;
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
