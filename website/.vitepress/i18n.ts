// Language support shared by the three Macro Grid sites (server, phone app, plugins).
// This file is copied byte-for-byte into each repo. Keep the copies identical.
//
// English lives at the site root, Turkish under /tr/ (VitePress `locales`). The visitor's choice is stored in
// localStorage; the three sites share one origin (deccoyi.github.io), so the choice carries over between them.
// Without a stored choice, Turkey (time zone Europe/Istanbul, or a Turkish browser) gets Turkish, everyone else English.

export type Lang = 'en' | 'tr'

export const LANG_KEY = 'mg-lang'
export const SITE_ORIGIN = 'https://deccoyi.github.io'

/** Language of a VitePress `lang` value such as 'tr-TR' or 'en-US'. */
export function langOf(vitepressLang: string): Lang {
  return vitepressLang.toLowerCase().startsWith('tr') ? 'tr' : 'en'
}

/** Best guess for a first-time visitor. Browser only. */
export function detectLang(): Lang {
  try {
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone
    if (tz === 'Europe/Istanbul' || tz === 'Asia/Istanbul') return 'tr'
  } catch { /* no Intl: fall through to the browser language */ }
  return /^tr\b/i.test(navigator.language || '') ? 'tr' : 'en'
}

export function readStoredLang(): Lang | null {
  try {
    const v = localStorage.getItem(LANG_KEY)
    return v === 'tr' || v === 'en' ? v : null
  } catch {
    return null
  }
}

export function storeLang(lang: Lang): void {
  try {
    localStorage.setItem(LANG_KEY, lang)
  } catch { /* private mode or blocked storage: the choice just is not remembered */ }
}

/** Maps a full URL path (base included) to the same page in the other language. `base` is like '/macro-grid/'. */
export function switchPath(path: string, base: string, to: Lang): string {
  const root = base.endsWith('/') ? base : base + '/'
  let rel = path.startsWith(root) ? path.slice(root.length - 1) : path // keeps the leading slash
  rel = rel.replace(/^\/tr(?=\/|$)/, '') || '/'
  if (to === 'en') return root.slice(0, -1) + rel
  return root.slice(0, -1) + '/tr' + (rel === '/' ? '/' : rel)
}

/** Link to a page of a sibling site in the given language. `siteBase` like '/macro-grid-plugin/', `page` like 'store/'. */
export function siteHref(siteBase: string, page: string, lang: Lang): string {
  return `${SITE_ORIGIN}${siteBase}${lang === 'tr' ? 'tr/' : ''}${page}`
}

/**
 * Inline script for <head>: on a first visit (or when the stored choice differs from the page language) it jumps to the
 * right language before the page paints, so there is no flash of the wrong language.
 */
export function earlyLangScript(base: string): string {
  const b = JSON.stringify(base.endsWith('/') ? base : base + '/')
  return (
    `(function(){try{var b=${b},p=location.pathname;if(p.indexOf(b)!==0)return;` +
    `var r=p.slice(b.length-1),tr=/^\\/tr(\\/|$)/.test(r),s=localStorage.getItem('${LANG_KEY}'),w=s;` +
    `if(w!=='tr'&&w!=='en'){var z='';try{z=Intl.DateTimeFormat().resolvedOptions().timeZone}catch(e){}` +
    `w=(z==='Europe/Istanbul'||z==='Asia/Istanbul'||/^tr\\b/i.test(navigator.language||''))?'tr':'en'}` +
    `if((w==='tr')===tr)return;` +
    `var n=w==='tr'?'/tr'+(r==='/'?'/':r):(r.replace(/^\\/tr(?=\\/|$)/,'')||'/');` +
    `location.replace(b.slice(0,-1)+n+location.search+location.hash)}catch(e){}})();`
  )
}

// Texts of the shared theme pieces (banner, product switcher, language switch, footer). Page-specific texts live next
// to their component or in the content files.
export const UI = {
  en: {
    aiStrong: 'Alpha software, written entirely by AI.',
    aiLong:
      'Nothing has been reviewed line by line by a human or security-audited. No warranty, no liability: you use it at your own risk.',
    aiMore: 'Read more',
    switcherAria: 'Macro Grid products',
    productPc: 'PC',
    productPhone: 'Phone',
    productPlugins: 'Plugins',
    langAria: 'Language',
    langTitleToTr: 'Switch to Turkish',
    langTitleToEn: 'Switch to English',
    footerSites: 'Macro Grid sites:',
    footerLicense:
      'Released under the MIT License. Alpha software, written entirely by an AI assistant, provided as is without warranty.',
  },
  tr: {
    aiStrong: 'Alfa yazılım, tamamen yapay zekâ tarafından yazıldı.',
    aiLong:
      'Hiçbir satır bir insan tarafından tek tek incelenmedi, güvenlik denetiminden geçmedi. Garanti ve sorumluluk yoktur: kullanım riski size aittir.',
    aiMore: 'Devamı',
    switcherAria: 'Macro Grid ürünleri',
    productPc: 'PC',
    productPhone: 'Telefon',
    productPlugins: 'Eklentiler',
    langAria: 'Dil',
    langTitleToTr: 'Türkçeye geç',
    langTitleToEn: 'İngilizceye geç',
    footerSites: 'Macro Grid siteleri:',
    footerLicense:
      'MIT Lisansı ile yayımlanmıştır. Alfa yazılım, tamamen bir yapay zekâ asistanı tarafından yazılmıştır, garanti verilmeden olduğu gibi sunulur.',
  },
} as const
