import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import AiBanner from './AiBanner.vue'
import HeroImage from './HeroImage.vue'
import './custom.css'
import { h } from 'vue'

export default {
  extends: DefaultTheme,
  Layout() {
    return h(DefaultTheme.Layout, null, {
      'layout-top': () => h(AiBanner),
      'home-hero-image': () => h(HeroImage),
    })
  },
} satisfies Theme
