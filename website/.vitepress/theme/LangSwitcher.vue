<script setup lang="ts">
import { computed } from 'vue'
import { useData, useRoute, useRouter } from 'vitepress'
import { UI, langOf, storeLang, switchPath } from '../i18n'

// On/off switch for the page language, styled like VitePress' dark mode switch. Off = English, on = Turkish.
// `bar` sits in the header next to the appearance switch, `screen` in the mobile menu.
defineProps<{ placement: 'bar' | 'screen' }>()

const { site, lang } = useData()
const route = useRoute()
const router = useRouter()

const current = computed(() => langOf(lang.value))
const t = computed(() => UI[current.value])

function toggle() {
  const to = current.value === 'tr' ? 'en' : 'tr'
  storeLang(to)
  router.go(switchPath(route.path, site.value.base, to) + window.location.search + window.location.hash)
}
</script>

<template>
  <div class="lang-row" :class="placement">
    <span v-if="placement === 'screen'" class="label">{{ t.langAria }}</span>
    <button
    type="button"
    class="lang-switch"
    :class="[placement, { tr: current === 'tr' }]"
    role="switch"
    :aria-checked="current === 'tr'"
    :aria-label="t.langAria"
    :title="current === 'tr' ? t.langTitleToEn : t.langTitleToTr"
    @click="toggle"
  >
    <span class="ends" aria-hidden="true"><span>EN</span><span>TR</span></span>
    <span class="knob" aria-hidden="true">{{ current === 'tr' ? 'TR' : 'EN' }}</span>
    </button>
  </div>
</template>

<style scoped>
.lang-switch {
  position: relative;
  display: inline-block;
  flex-shrink: 0;
  width: 50px;
  height: 24px;
  padding: 0;
  border: 1px solid var(--vp-input-border-color);
  border-radius: 12px;
  background-color: var(--vp-input-switch-bg-color);
  cursor: pointer;
  transition: border-color 0.25s, background-color 0.25s;
}
.lang-switch:hover { border-color: var(--vp-c-brand-1); }
.lang-switch:focus-visible { outline: 2px solid var(--vp-c-brand-1); outline-offset: 2px; }
/* The two language codes sit in the track; the knob covers the active one. */
.ends {
  position: absolute;
  inset: 0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 7px;
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.02em;
  color: var(--vp-c-text-3);
}
.knob {
  position: absolute;
  top: 1px;
  left: 1px;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 20px;
  border-radius: 10px;
  background-color: var(--vp-c-neutral-inverse);
  box-shadow: var(--vp-shadow-1);
  color: var(--vp-c-text-1);
  font-size: 10px;
  font-weight: 700;
  transition: transform 0.25s;
}
.lang-switch.tr .knob { transform: translateX(24px); }
.lang-row.bar { display: flex; align-items: center; margin-left: 12px; }
@media (max-width: 767px) {
  .lang-row.bar { display: none; }
}
/* Mobile menu: a row like the "Appearance" row above it. */
.lang-row.screen {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 12px;
  padding: 12px 14px 12px 16px;
  border-radius: 8px;
  background-color: var(--vp-c-bg-soft);
}
.lang-row .label { font-size: 12px; font-weight: 500; color: var(--vp-c-text-2); }
</style>
