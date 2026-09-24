import { defineConfig } from 'vitepress'

const repo = 'https://github.com/Deccoyi/macro-grid'
const pluginSite = 'https://deccoyi.github.io/macro-grid-plugin/'
const clientSite = 'https://deccoyi.github.io/macro-grid-client/'

export default defineConfig({
  title: 'Macro Grid',
  description: 'Turn a phone or tablet into a customizable macro deck for your Windows PC. Features, tutorials and help.',
  base: '/macro-grid/',
  lang: 'en-US',
  cleanUrls: true,
  lastUpdated: false,
  appearance: true,
  head: [
    ['link', { rel: 'icon', type: 'image/png', href: '/macro-grid/favicon.png' }],
    ['meta', { name: 'theme-color', content: '#d97706' }],
  ],

  themeConfig: {
    logo: '/logo.png',
    search: { provider: 'local' },
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
          { text: 'Plugin store', link: pluginSite + 'store/' },
          { text: 'Phone app', link: clientSite },
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
    outline: { level: [2, 3] },
    footer: {
      message: 'Macro Grid sites: <a href="https://deccoyi.github.io/macro-grid/">Server</a> &middot; <a href="https://deccoyi.github.io/macro-grid-client/">Phone app</a> &middot; <a href="https://deccoyi.github.io/macro-grid-plugin/">Plugins</a><br>Released under the MIT License. Alpha software, written entirely by an AI assistant, provided as is without warranty.',
      copyright: 'Copyright (c) 2026 Deccoyi',
    },
  },
})
