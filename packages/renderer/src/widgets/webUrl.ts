/** The longest address a web widget accepts. */
export const MAX_WEB_URL_LENGTH = 2048;

/**
 * Which addresses a `web` widget (or a `core.web` button) may show. The page is untrusted content from the
 * internet: only `http` and `https`, no user name or password in the address, and never this app's own origin
 * or any loopback address (such a page would be same-origin with the app and could read its storage).
 * The check runs on the parsed, normalized address, never on the raw text: the URL parser already turns
 * `0x7f.1` or `2130706433` into `127.0.0.1`. The server applies the same rule when a profile is saved or imported
 * (`WebUrlRule`); keep them in step.
 *
 * `blockedHosts` adds host names that must be refused too, for example the server's own address on the phone.
 */
export function isSafeWebUrl(raw: unknown, blockedHosts: readonly string[] = []): raw is string {
  if (typeof raw !== "string" || raw.length === 0 || raw.length > MAX_WEB_URL_LENGTH || raw !== raw.trim()) return false;
  if (raw.includes("\\")) return false;

  let url: URL;
  try {
    url = new URL(raw);
  } catch {
    return false;
  }
  if (url.protocol !== "http:" && url.protocol !== "https:") return false;
  if (url.username !== "" || url.password !== "") return false;

  const host = url.hostname.toLowerCase();
  if (host === "" || host.endsWith(".")) return false;
  if (isLoopbackHost(host)) return false;
  if (blockedHosts.some((h) => h.toLowerCase() === host)) return false;
  return !isOwnOrigin(url);
}

/** The address of a page that may be shown in the app's own origin (a page served by the app itself). */
function isOwnOrigin(url: URL): boolean {
  try {
    return typeof location !== "undefined" && (url.origin === location.origin || url.hostname === location.hostname);
  } catch {
    return false;
  }
}

function isLoopbackHost(host: string): boolean {
  if (host === "localhost" || host.endsWith(".localhost")) return true;
  if (/^\d{1,3}(\.\d{1,3}){3}$/.test(host)) {
    const first = Number(host.split(".")[0] ?? "");
    return first === 127 || first === 0;
  }
  if (host.startsWith("[") && host.endsWith("]")) return isLoopbackV6(host.slice(1, -1));
  return false;
}

/** `::1`, `::` and the IPv4-mapped or IPv4-compatible forms of a loopback or unspecified address, however they are written. */
function isLoopbackV6(address: string): boolean {
  const groups = expandV6(address);
  if (!groups) return true; // cannot be read: refuse
  const zeros = groups.slice(0, 5).every((g) => g === 0);
  if (!zeros) return false;
  // ::, ::1, and ::ffff:a.b.c.d / ::a.b.c.d with a.b.c.d loopback or unspecified
  if (groups[5] === 0 && groups[6] === 0 && (groups[7] === 0 || groups[7] === 1)) return true;
  if (groups[5] === 0xffff || groups[5] === 0) {
    const firstOctet = (groups[6] ?? 0) >> 8;
    return firstOctet === 127 || firstOctet === 0;
  }
  return false;
}

function expandV6(address: string): number[] | null {
  const halves = address.split("::");
  if (halves.length > 2) return null;
  const parse = (part: string): number[] | null => {
    if (part === "") return [];
    const out: number[] = [];
    for (const g of part.split(":")) {
      if (!/^[0-9a-f]{1,4}$/i.test(g)) return null;
      out.push(parseInt(g, 16));
    }
    return out;
  };
  const head = parse(halves[0] ?? "");
  const tail = halves.length === 2 ? parse(halves[1] ?? "") : [];
  if (!head || !tail) return null;
  if (halves.length === 1) return head.length === 8 ? head : null;
  const missing = 8 - head.length - tail.length;
  if (missing < 1) return null;
  return [...head, ...new Array<number>(missing).fill(0), ...tail];
}

/** A safe-to-show label for an address: its host name only, never the path or query (which may hold a secret token). */
export function webUrlHost(raw: string): string {
  try {
    return new URL(raw).hostname;
  } catch {
    return "";
  }
}
