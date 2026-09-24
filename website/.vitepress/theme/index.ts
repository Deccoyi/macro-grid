import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import AiBanner from './AiBanner.vue'
import HeroImage from './HeroImage.vue'
import SiteSwitcher from './SiteSwitcher.vue'
import DownloadList from './DownloadList.vue'
import './shared.css'
import './accent.css'
import { h } from 'vue'

export default {
  extends: DefaultTheme,
  Layout() {
    return h(DefaultTheme.Layout, null, {
      'layout-top': () => h(AiBanner),
      'nav-bar-content-before': () => h(SiteSwitcher, { placement: 'bar' }),
      'nav-screen-content-before': () => h(SiteSwitcher, { placement: 'screen' }),
      'home-hero-image': () => h(HeroImage),
    })
  },
  enhanceApp({ app }) {
    app.component('DownloadList', DownloadList)
  },
} satisfies Theme
