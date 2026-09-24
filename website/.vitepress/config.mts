import { defineConfig } from 'vitepress'
import { sharedConfig, turkishThemeLabels } from './shared'

const repo = 'https://github.com/Deccoyi/macro-grid'
const shared = sharedConfig('/macro-grid/', '#d97706')

export default defineConfig({
  ...shared,
  title: 'Macro Grid',
  description: 'Turn a phone or tablet into a customizable macro deck for your Windows PC. Features, tutorials and help.',
  base: '/macro-grid/',

  locales: {
    root: { label: 'English', lang: 'en-US' },
    tr: {
      label: 'Türkçe',
      lang: 'tr-TR',
      link: '/tr/',
      description: "Telefonunuzu veya tabletinizi Windows bilgisayarınız için özelleştirilebilir bir makro deck'ine dönüştürün. Özellikler, öğreticiler ve yardım.",
      themeConfig: {
        ...turkishThemeLabels(),
        nav: [
          { text: 'Kılavuz', link: '/tr/guide/', activeMatch: '/tr/guide/' },
          {
            text: 'Öğren',
            activeMatch: '^/tr/(tutorials|reference)/',
            items: [
              { text: 'Öğreticiler', link: '/tr/tutorials/volume-slider' },
              { text: 'Başvuru', link: '/tr/reference/actions' },
              { text: 'SSS', link: '/tr/reference/faq' },
            ],
          },
          { text: 'İndir', link: '/tr/download' },
          {
            text: 'Ekosistem',
            activeMatch: '/tr/developers/|/tr/ecosystem',
            items: [
              { text: 'Genel bakış', link: '/tr/ecosystem' },
              { text: 'Geliştiriciler için', link: '/tr/developers/' },
            ],
          },
        ],
        sidebar: {
          '/tr/developers/': [
            {
              text: 'Geliştiriciler için',
              items: [
                { text: 'Genel bakış', link: '/tr/developers/' },
                { text: 'Kaynaktan derleme', link: '/tr/developers/build' },
                { text: 'Teknik başvuru', link: '/tr/developers/reference' },
              ],
            },
          ],
          '/tr/': [
            {
              text: 'Başlarken',
              items: [
                { text: 'Hızlı başlangıç', link: '/tr/guide/' },
                { text: 'Sunucuyu kurun', link: '/tr/guide/install' },
                { text: 'Cihaz eşleştirme', link: '/tr/guide/pairing' },
                { text: 'Telefon uygulaması', link: '/tr/guide/phone-app' },
              ],
            },
            {
              text: "Deck tasarlama",
              items: [
                { text: 'Düzenleyici', link: '/tr/guide/editor' },
                { text: "Widget'lar", link: '/tr/guide/widgets' },
                { text: 'Aksiyonlar ve makrolar', link: '/tr/guide/actions' },
                { text: 'Değişkenler ve metin', link: '/tr/guide/variables' },
                { text: 'Dinamik kurallar', link: '/tr/guide/dynamic' },
                { text: 'Stil, simgeler ve CSS', link: '/tr/guide/styling' },
              ],
            },
            {
              text: 'Yönetim',
              items: [
                { text: 'Profiller ve sayfalar', link: '/tr/guide/profiles' },
                { text: 'Uygulamaya göre otomatik geçiş', link: '/tr/guide/auto-switch' },
                { text: 'Eklentiler', link: '/tr/guide/plugins' },
                { text: 'Tercihler', link: '/tr/guide/preferences' },
                { text: 'Güvenlik', link: '/tr/guide/security' },
                { text: 'Sorun giderme', link: '/tr/guide/troubleshooting' },
              ],
            },
            {
              text: 'Öğreticiler',
              items: [
                { text: "Ses slider'ı", link: '/tr/tutorials/volume-slider' },
                { text: 'Canlı CPU kutucuğu', link: '/tr/tutorials/cpu-tile' },
                { text: "OBS ile yayın deck'i", link: '/tr/tutorials/obs-deck' },
                { text: 'Her uygulamaya bir profil', link: '/tr/tutorials/profile-per-app' },
              ],
            },
            {
              text: 'Başvuru',
              items: [
                { text: 'Aksiyonlar', link: '/tr/reference/actions' },
                { text: 'Değişkenler', link: '/tr/reference/variables' },
                { text: 'Kısayol tuş adları', link: '/tr/reference/keys' },
                { text: 'Dosyalar ve portlar', link: '/tr/reference/files' },
                { text: 'SSS', link: '/tr/reference/faq' },
              ],
            },
          ],
        },
        editLink: { pattern: `${repo}/edit/dev/website/:path`, text: "GitHub'da bir değişiklik öner" },
      },
    },
  },

  themeConfig: {
    ...shared.themeConfig,
    nav: [
      { text: 'Guide', link: '/guide/', activeMatch: '/guide/' },
      {
        text: 'Learn',
        activeMatch: '^/(tutorials|reference)/',
        items: [
          { text: 'Tutorials', link: '/tutorials/volume-slider' },
          { text: 'Reference', link: '/reference/actions' },
          { text: 'FAQ', link: '/reference/faq' },
        ],
      },
      { text: 'Download', link: '/download' },
      {
        text: 'Ecosystem',
        activeMatch: '/developers/|/ecosystem',
        items: [
          { text: 'Overview', link: '/ecosystem' },
          { text: 'For developers', link: '/developers/' },
        ],
      },
    ],
    sidebar: {
      '/developers/': [
        {
          text: 'For developers',
          items: [
            { text: 'Overview', link: '/developers/' },
            { text: 'Build from source', link: '/developers/build' },
            { text: 'Technical reference', link: '/developers/reference' },
          ],
        },
      ],
      '/': [
      {
        text: 'Getting started',
        items: [
          { text: 'Quick start', link: '/guide/' },
          { text: 'Install the server', link: '/guide/install' },
          { text: 'Pair a device', link: '/guide/pairing' },
          { text: 'The phone app', link: '/guide/phone-app' },
        ],
      },
      {
        text: 'Designing decks',
        items: [
          { text: 'The editor', link: '/guide/editor' },
          { text: 'Widgets', link: '/guide/widgets' },
          { text: 'Actions and macros', link: '/guide/actions' },
          { text: 'Variables and text', link: '/guide/variables' },
          { text: 'Dynamic rules', link: '/guide/dynamic' },
          { text: 'Styling, icons and CSS', link: '/guide/styling' },
        ],
      },
      {
        text: 'Managing',
        items: [
          { text: 'Profiles and pages', link: '/guide/profiles' },
          { text: 'Auto-switching by app', link: '/guide/auto-switch' },
          { text: 'Plugins', link: '/guide/plugins' },
          { text: 'Preferences', link: '/guide/preferences' },
          { text: 'Security', link: '/guide/security' },
          { text: 'Troubleshooting', link: '/guide/troubleshooting' },
        ],
      },
      {
        text: 'Tutorials',
        items: [
          { text: 'A volume slider', link: '/tutorials/volume-slider' },
          { text: 'A live CPU tile', link: '/tutorials/cpu-tile' },
          { text: 'A streaming deck with OBS', link: '/tutorials/obs-deck' },
          { text: 'A profile per app', link: '/tutorials/profile-per-app' },
        ],
      },
      {
        text: 'Reference',
        items: [
          { text: 'Actions', link: '/reference/actions' },
          { text: 'Variables', link: '/reference/variables' },
          { text: 'Shortcut key names', link: '/reference/keys' },
          { text: 'Files and ports', link: '/reference/files' },
          { text: 'FAQ', link: '/reference/faq' },
        ],
      },
      ],
    },
    socialLinks: [{ icon: 'github', link: repo }],
    editLink: { pattern: `${repo}/edit/dev/website/:path`, text: 'Suggest a change on GitHub' },
  },
})
