// Settings shared by the three Macro Grid sites (server, phone app, plugins).
// This file is copied byte-for-byte into each repo. Keep the copies identical.
export const SITE_ORIGIN = 'https://deccoyi.github.io/'

// Links between the sibling sites open in the same tab. VitePress adds target="_blank" to every
// external link in markdown pages, and its router hijacks any same-origin link that has no target
// attribute (it would look for the page inside the current site and show a 404). So the sibling
// links get an explicit target="_self": the router leaves them alone and the browser navigates.
export function sameTabForSiblingSites(md: any): void {
  const previous = md.renderer.rules.link_open
  md.renderer.rules.link_open = (tokens: any[], idx: number, options: any, env: any, self: any) => {
    const href: string = tokens[idx].attrGet('href') ?? ''
    const html: string = previous
      ? previous(tokens, idx, options, env, self)
      : self.renderToken(tokens, idx, options)
    if (!href.startsWith(SITE_ORIGIN)) return html
    return html.replace(/\s(?:target|rel)="[^"]*"/g, '').replace(/^<a\b/, '<a target="_self"')
  }
}

const footerMessage =
  'Macro Grid sites: <a target="_self" href="https://deccoyi.github.io/macro-grid/">PC</a> &middot; ' +
  '<a target="_self" href="https://deccoyi.github.io/macro-grid-client/">Phone</a> &middot; ' +
  '<a target="_self" href="https://deccoyi.github.io/macro-grid-plugin/store/">Plugins</a><br>' +
  'Released under the MIT License. Alpha software, written entirely by an AI assistant, provided as is without warranty.'

export function sharedConfig(base: string, themeColor: string) {
  return {
    lang: 'en-US',
    cleanUrls: true,
    lastUpdated: false,
    appearance: true,
    head: [
      ['link', { rel: 'icon', type: 'image/png', href: `${base}favicon.png` }],
      ['meta', { name: 'theme-color', content: themeColor }],
    ] as [string, Record<string, string>][],
    markdown: { config: sameTabForSiblingSites },
    themeConfig: {
      logo: '/logo.png',
      siteTitle: 'Macro Grid',
      search: { provider: 'local' as const },
      outline: { level: [2, 3] as [number, number] },
      externalLinkIcon: false,
      footer: {
        message: footerMessage,
        copyright: 'Copyright (c) 2026 Deccoyi',
      },
    },
  }
}
