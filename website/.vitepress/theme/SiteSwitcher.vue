<script setup lang="ts">
import { computed } from 'vue'
import { useData } from 'vitepress'

// Segmented control that moves between the three Macro Grid sites, always in the same tab.
// `bar` is shown in the header on wide screens, `screen` in the mobile menu.
defineProps<{ placement: 'bar' | 'screen' }>()

const { site } = useData()

const products = [
  { label: 'PC', base: '/macro-grid/', href: 'https://deccoyi.github.io/macro-grid/' },
  { label: 'Phone', base: '/macro-grid-client/', href: 'https://deccoyi.github.io/macro-grid-client/' },
  { label: 'Plugins', base: '/macro-grid-plugin/', href: 'https://deccoyi.github.io/macro-grid-plugin/store/' },
]

// Compare with the trailing slash: '/macro-grid/' is a prefix of the other two bases.
const currentBase = computed(() => (site.value.base.endsWith('/') ? site.value.base : site.value.base + '/'))
</script>

<template>
  <nav class="site-switcher" :class="placement" aria-label="Macro Grid products">
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
