// Settings shared by the three Macro Grid sites (server, phone app, plugins).
// This file is copied byte-for-byte into each repo. Keep the copies identical.
import { SITE_ORIGIN, UI, earlyLangScript, type Lang } from './i18n'

export { SITE_ORIGIN }

const SIBLING_SITE = new RegExp(`^${SITE_ORIGIN.replace(/\./g, '\\.')}/(macro-grid(?:-client|-plugin)?)/(?!tr(?:/|$))`)

// Links between the sibling sites open in the same tab. VitePress adds target="_blank" to every
// external link in markdown pages, and its router hijacks any same-origin link that has no target
// attribute (it would look for the page inside the current site and show a 404). So the sibling
// links get an explicit target="_self": the router leaves them alone and the browser navigates.
// On the Turkish pages (tr/...) the sibling links also point to the Turkish version of the other site.
export function sameTabForSiblingSites(md: any): void {
  const previous = md.renderer.rules.link_open
  md.renderer.rules.link_open = (tokens: any[], idx: number, options: any, env: any, self: any) => {
    const href: string = tokens[idx].attrGet('href') ?? ''
    const html: string = previous
      ? previous(tokens, idx, options, env, self)
      : self.renderToken(tokens, idx, options)
    if (!href.startsWith(SITE_ORIGIN + '/')) return html
    if (/^tr(\/|$)/.test(env?.relativePath ?? '') && SIBLING_SITE.test(href)) {
      tokens[idx].attrSet('href', href.replace(SIBLING_SITE, `${SITE_ORIGIN}/$1/tr/`))
    }
    const fixed = html.replace(/\shref="[^"]*"/, ` href="${tokens[idx].attrGet('href')}"`)
    return fixed.replace(/\s(?:target|rel)="[^"]*"/g, '').replace(/^<a\b/, '<a target="_self"')
  }
}

export function footerFor(lang: Lang) {
  const t = UI[lang]
  const tr = lang === 'tr' ? 'tr/' : ''
  return {
    message:
      `${t.footerSites} <a target="_self" href="${SITE_ORIGIN}/macro-grid/${tr}">${t.productPc}</a> &middot; ` +
      `<a target="_self" href="${SITE_ORIGIN}/macro-grid-client/${tr}">${t.productPhone}</a> &middot; ` +
      `<a target="_self" href="${SITE_ORIGIN}/macro-grid-plugin/${tr}store/">${t.productPlugins}</a><br>` +
      t.footerLicense,
    copyright: 'Copyright (c) 2026 Deccoyi',
  }
}

/** Turkish labels of the default VitePress theme. Merge into `locales.tr.themeConfig` together with nav and sidebar. */
export function turkishThemeLabels() {
  return {
    outline: { label: 'Bu sayfada', level: [2, 3] as [number, number] },
    docFooter: { prev: 'Önceki sayfa', next: 'Sonraki sayfa' },
    darkModeSwitchLabel: 'Görünüm',
    lightModeSwitchTitle: 'Açık temaya geç',
    darkModeSwitchTitle: 'Koyu temaya geç',
    sidebarMenuLabel: 'Menü',
    returnToTopLabel: 'Başa dön',
    langMenuLabel: 'Dili değiştir',
    skipToContentLabel: 'İçeriğe geç',
    externalLinkIcon: false,
    footer: footerFor('tr'),
  }
}

const turkishSearch = {
  translations: {
    button: { buttonText: 'Ara', buttonAriaLabel: 'Ara' },
    modal: {
      displayDetails: 'Ayrıntıları göster',
      resetButtonTitle: 'Aramayı temizle',
      backButtonTitle: 'Aramayı kapat',
      noResultsText: 'Sonuç bulunamadı:',
      footer: {
        selectText: 'seç',
        selectKeyAriaLabel: 'enter',
        navigateText: 'gezin',
        navigateUpKeyAriaLabel: 'yukarı ok',
        navigateDownKeyAriaLabel: 'aşağı ok',
        closeText: 'kapat',
        closeKeyAriaLabel: 'esc',
      },
    },
  },
}

// Every page announces its other-language twin to search engines. Generated pages (store/[id]) are skipped.
function alternateLinks(base: string) {
  return (pageData: any) => {
    if (pageData.params || pageData.isNotFound) return
    const rel: string = pageData.relativePath.replace(/(^|\/)index\.md$/, '$1').replace(/\.md$/, '')
    const plain = rel.replace(/^tr(?:\/|$)/, '')
    const en = `${SITE_ORIGIN}${base}${plain}`
    const tr = `${SITE_ORIGIN}${base}tr/${plain}`
    const head = (pageData.frontmatter.head ??= [])
    head.push(
      ['link', { rel: 'alternate', hreflang: 'en', href: en }],
      ['link', { rel: 'alternate', hreflang: 'tr', href: tr }],
      ['link', { rel: 'alternate', hreflang: 'x-default', href: en }],
    )
  }
}

export function sharedConfig(base: string, themeColor: string) {
  return {
    lang: 'en-US',
    cleanUrls: true,
    lastUpdated: false,
    appearance: true,
    head: [
      ['link', { rel: 'icon', type: 'image/png', href: `${base}favicon.png` }],
      ['meta', { name: 'theme-color', content: themeColor }],
      ['script', {}, earlyLangScript(base)],
    ] as [string, Record<string, string>, string?][],
    markdown: { config: sameTabForSiblingSites },
    transformPageData: alternateLinks(base),
    themeConfig: {
      logo: '/logo.png',
      siteTitle: 'Macro Grid',
      search: { provider: 'local' as const, options: { locales: { tr: turkishSearch } } },
      outline: { level: [2, 3] as [number, number] },
      externalLinkIcon: false,
      footer: footerFor('en'),
    },
  }
}
