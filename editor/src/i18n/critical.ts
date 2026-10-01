/**
 * Texts a person decides on: what a plugin may do, what an import brings, removing, pairing. While a language pack is active they are
 * shown as "<translation> (<English>)", so a pack can never make a permission or a removal sound milder than it is. One place; no call
 * site can forget it. A key is critical when it starts with one of these prefixes.
 */
const CRITICAL_PREFIXES = [
  "consent.",
  "plugins.permission.",
  "plugins.permissions.",
  "plugins.approve",
  "plugins.install.jsPermissions.",
  "plugins.install.nativeWarning.",
  "plugins.uninstall",
  "plugins.discover.thirdParty.",
  "palette.unverified",
  "importWeb.",
  "profile.importWeb",
  "pairing.revoke",
] as const;

export function isCritical(key: string): boolean {
  return CRITICAL_PREFIXES.some((prefix) => key.startsWith(prefix));
}
