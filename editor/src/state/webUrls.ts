import { webUrlHost, type Profile } from "@macro/renderer";

/** Every address a profile would open on a phone: the `web` widgets' own and the ones set by "Change web page" buttons. Each entry can be cleared with `clearWebUrls`. */
function eachWebUrl(profile: Profile, visit: (url: string, clear: () => void) => void): void {
  for (const page of profile.pages) {
    for (const widget of page.widgets) {
      const props = widget.props;
      if (widget.type === "web" && props && typeof props.url === "string" && props.url !== "") visit(props.url, () => { delete props.url; });
      for (const bindings of Object.values(widget.actions)) {
        for (const binding of bindings ?? []) {
          const settings = binding.settings;
          if (binding.type === "core.web" && settings.mode === "set" && typeof settings.url === "string" && settings.url !== "")
            visit(settings.url, () => { settings.url = ""; });
        }
      }
    }
  }
}

/** Like `collectWebHosts`, with whether each site is reached over an encrypted connection (a host reached both ways counts as not encrypted). */
export function collectWebSites(profile: Profile): { host: string; secure: boolean }[] {
  const sites = new Map<string, boolean>();
  eachWebUrl(profile, (url) => {
    const host = webUrlHost(url) || "?";
    sites.set(host, (sites.get(host) ?? true) && /^https:/i.test(url));
  });
  return [...sites].map(([host, secure]) => ({ host, secure }));
}

/** The host names (never the path or query, which may hold a secret) of the addresses in a profile, without duplicates. */
export function collectWebHosts(profile: Profile): string[] {
  const hosts = new Set<string>();
  eachWebUrl(profile, (url) => hosts.add(webUrlHost(url) || "?"));
  return [...hosts];
}

/** Empties every web address of the profile (the widgets and buttons stay). Changes the profile in place. */
export function clearWebUrls(profile: Profile): void {
  eachWebUrl(profile, (_url, clear) => clear());
}
