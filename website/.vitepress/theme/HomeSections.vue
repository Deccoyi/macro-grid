<script setup lang="ts">
import { withBase } from 'vitepress'
const steps = [
  ['1', 'Install the server', 'Run the Windows installer. Macro Grid lives in the tray.'],
  ['2', 'Pair a device', 'Scan the QR code or type the six-digit PIN. Any browser works too.'],
  ['3', 'Design your deck', 'Drag widgets on a grid, bind actions, style them, save. The phone updates at once.'],
]
const parts = [
  ['Server and editor', 'Windows tray app that runs actions on your PC and hosts the editor.', 'https://github.com/Deccoyi/macro-grid', 'macro-grid'],
  ['Phone and tablet app', 'Android app that draws the deck full screen and sends your touches.', 'https://github.com/Deccoyi/macro-grid-client', 'macro-grid-client'],
  ['Plugins', 'OBS control, icon packs and your own plugins in JavaScript or C#.', 'https://deccoyi.github.io/macro-grid-plugin/', 'Plugin docs site'],
]
</script>

<template>
  <section class="wrap">
    <h2>Up and running in three steps</h2>
    <div class="steps">
      <div v-for="s in steps" :key="s[0]" class="card">
        <span class="n">{{ s[0] }}</span><h3>{{ s[1] }}</h3><p>{{ s[2] }}</p>
      </div>
    </div>
    <p class="cta"><a :href="withBase('/guide/')">Read the quick start →</a></p>

    <h2>Three parts, one system</h2>
    <div class="steps">
      <a v-for="p in parts" :key="p[0]" class="card link" :href="p[2]">
        <h3>{{ p[0] }}</h3><p>{{ p[1] }}</p><small>{{ p[3] }} ↗</small>
      </a>
    </div>

    <p class="warn">
      <strong>Alpha, AI-generated, at your own risk.</strong> Macro Grid is early software. It is meant for a home or
      office network you trust, not the internet. See <a :href="withBase('/guide/security')">Security</a>.
    </p>
  </section>
</template>

<style scoped>
.wrap { max-width: 1152px; margin: 0 auto; padding: 24px 24px 64px; }
h2 { border: 0; margin: 40px 0 16px; text-align: center; font-size: 26px; letter-spacing: -.01em; }
.steps { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; }
.card { background: var(--vp-c-bg-soft); border: 1px solid var(--vp-c-divider); border-radius: 12px; padding: 20px; }
.card h3 { margin: 6px 0 6px; font-size: 17px; }
.card p { margin: 0; color: var(--vp-c-text-2); font-size: 14.5px; line-height: 1.6; }
.card small { display: block; margin-top: 10px; color: var(--vp-c-brand-1); font-weight: 600; }
.link { text-decoration: none; color: inherit; transition: border-color .2s; }
.link:hover { border-color: var(--vp-c-brand-2); }
.n { display: inline-grid; place-items: center; width: 28px; height: 28px; border-radius: 50%;
  background: var(--vp-c-brand-soft); color: var(--vp-c-brand-1); font-weight: 700; }
.cta { text-align: center; margin-top: 20px; font-weight: 600; }
.warn { margin-top: 40px; padding: 14px 18px; border-radius: 10px; border: 1px solid var(--vp-c-warning-2, #d97706);
  background: var(--vp-c-warning-soft, rgba(217,119,6,.12)); font-size: 14px; }
</style>
