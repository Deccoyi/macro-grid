/** Where a network plugin permission (`http:host:port`, later `ws:host:port`) points: this PC, the local network, or the internet.
 * Only a label for the approval text; the server enforces the exact host and port and the real address either way.
 * The server has the same rules (JsNetworkGuard.ScopeOf); tests/shared/network-scope-cases.json keeps the two in step. */
export type HttpTargetScope = "local" | "lan" | "internet";

export function networkTargetScope(target: string): HttpTargetScope {
  const host = target.replace(/:\d+$/, "").replace(/^\[|\]$/g, "").toLowerCase();

  if (host === "localhost" || host.endsWith(".localhost") || host === "::1" || /^127\./.test(host)) return "local";

  const v4 = host.match(/^(\d{1,3})\.(\d{1,3})\.\d{1,3}\.\d{1,3}$/);
  if (v4) {
    const [a, b] = [Number(v4[1]), Number(v4[2])];
    if (a === 10 || (a === 192 && b === 168) || (a === 172 && b >= 16 && b <= 31) || (a === 169 && b === 254) || (a === 100 && b >= 64 && b <= 127)) return "lan";
    return "internet";
  }

  // A single-label name ("nas") or a local-only suffix is resolved on the local network.
  if (!host.includes(".") || /\.(local|lan|home\.arpa|internal)$/.test(host)) return "lan";
  return "internet";
}
