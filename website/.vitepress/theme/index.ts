import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import DeckMockup from './DeckMockup.vue'
import HomeSections from './HomeSections.vue'
import './custom.css'
import { h } from 'vue'

export default {
  extends: DefaultTheme,
  Layout() {
    return h(DefaultTheme.Layout, null, {
      'home-hero-image': () => h(DeckMockup),
      'home-features-after': () => h(HomeSections),
    })
  },
} satisfies Theme
