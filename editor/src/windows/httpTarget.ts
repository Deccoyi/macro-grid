/** Where an <c>http:host:port</c> plugin permission points: this PC, the local network, or the internet.
 * Only a label for the approval text; the server enforces the exact host and port either way. */
export type HttpTargetScope = "local" | "lan" | "internet";

export function httpTargetScope(target: string): HttpTargetScope {
  const host = target.replace(/:\d+$/, "").replace(/^\[|\]$/g, "").toLowerCase();

  if (host === "localhost" || host === "::1" || /^127\./.test(host)) return "local";

  const v4 = host.match(/^(\d{1,3})\.(\d{1,3})\.\d{1,3}\.\d{1,3}$/);
  if (v4) {
    const [a, b] = [Number(v4[1]), Number(v4[2])];
    if (a === 10 || (a === 192 && b === 168) || (a === 172 && b >= 16 && b <= 31) || (a === 169 && b === 254)) return "lan";
    return "internet";
  }

  // A single-label name ("nas") or a local-only suffix is resolved on the local network.
  if (!host.includes(".") || /\.(local|lan|home\.arpa|internal)$/.test(host)) return "lan";
  return "internet";
}
