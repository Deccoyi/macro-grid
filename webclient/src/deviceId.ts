const KEY = "macro-station.webDeviceId";

/** A stable per-browser-profile id, same role as the Android client's deviceId — lets the server pair
 * this browser once and recognize it across reconnects/tab reloads. Generated once, kept in localStorage
 * (so a different browser, or this one in private mode, is a "new" device that needs its own PIN). */
export function getDeviceId(): string {
  let id = localStorage.getItem(KEY);
  if (!id) {
    id = crypto.randomUUID();
    localStorage.setItem(KEY, id);
  }
  return id;
}
