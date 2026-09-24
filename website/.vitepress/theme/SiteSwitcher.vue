<script setup lang="ts">
import { computed } from 'vue'
import { useData } from 'vitepress'
import { UI, langOf, siteHref } from '../i18n'

// Segmented control that moves between the three Macro Grid sites, always in the same tab.
// `bar` is shown in the header on wide screens, `screen` in the mobile menu.
defineProps<{ placement: 'bar' | 'screen' }>()

const { site, lang } = useData()
const current = computed(() => langOf(lang.value))
const t = computed(() => UI[current.value])

// The links keep the visitor's language: the Turkish pages of the other sites live under tr/.
const products = computed(() => [
  { label: t.value.productPc, base: '/macro-grid/', href: siteHref('/macro-grid/', '', current.value) },
  { label: t.value.productPhone, base: '/macro-grid-client/', href: siteHref('/macro-grid-client/', '', current.value) },
  { label: t.value.productPlugins, base: '/macro-grid-plugin/', href: siteHref('/macro-grid-plugin/', 'store/', current.value) },
])

// Compare with the trailing slash: '/macro-grid/' is a prefix of the other two bases.
const currentBase = computed(() => (site.value.base.endsWith('/') ? site.value.base : site.value.base + '/'))
</script>

<template>
  <nav class="site-switcher" :class="placement" :aria-label="t.switcherAria">
    <a
      v-for="p in products"
      :key="p.base"
      :href="p.href"
      target="_self"
      :class="{ current: p.base === currentBase }"
      :aria-current="p.base === currentBase ? 'true' : undefined"
    >{{ p.label }}</a>
  </nav>
</template>

<style scoped>
.site-switcher {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  padding: 2px;
  border: 1px solid var(--vp-c-divider);
  border-radius: 999px;
  background: var(--vp-c-bg-soft);
  white-space: nowrap;
}
.site-switcher a {
  padding: 2px 11px;
  border-radius: 999px;
  font-size: 13px;
  font-weight: 500;
  line-height: 22px;
  color: var(--vp-c-text-2);
  text-decoration: none;
  transition: color 0.2s, background-color 0.2s;
}
.site-switcher a:hover { color: var(--vp-c-text-1); }
.site-switcher a:focus-visible { outline: 2px solid var(--vp-c-brand-1); outline-offset: 1px; }
.site-switcher a.current,
.site-switcher a.current:hover {
  background: var(--vp-button-brand-bg);
  color: var(--vp-button-brand-text);
}
.bar { margin: 0 12px; }
.screen { display: none; margin: 0 0 20px; }
@media (min-width: 768px) and (max-width: 959px) {
  .bar { margin: 0 4px; }
  .bar a { padding: 2px 6px; font-size: 12px; }
}
@media (max-width: 767px) {
  .bar { display: none; }
  .screen { display: inline-flex; }
}
</style>
