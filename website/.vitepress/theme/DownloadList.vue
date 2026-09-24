<script setup lang="ts">
import { computed } from 'vue'
import { useData } from 'vitepress'
import { data } from '../releases.data'
import { langOf } from '../i18n'

const RELEASES = 'https://github.com/Deccoyi/macro-grid/releases'

const { lang } = useData()
const isTr = computed(() => langOf(lang.value) === 'tr')
const STR = {
  en: {
    latest: 'Latest version', alpha: 'alpha', os: 'Windows 10 or 11', download: 'Download for Windows',
    notes: 'Release notes', previous: 'Previous versions', dl: 'Download',
    olderBefore: 'Older versions are on the ', olderLink: 'GitHub Releases page', olderAfter: '.',
    none: 'Releases are not available right now', see: 'See GitHub Releases for the latest installer.', open: 'Open GitHub Releases',
  },
  tr: {
    latest: 'Son sürüm', alpha: 'alfa', os: 'Windows 10 veya 11', download: 'Windows için indir',
    notes: 'Sürüm notları', previous: 'Önceki sürümler', dl: 'İndir',
    olderBefore: 'Eski sürümler ', olderLink: 'GitHub Sürümler sayfasında', olderAfter: '.',
    none: 'Sürümlere şu anda ulaşılamıyor', see: 'En güncel kurulum dosyası için GitHub Sürümler sayfasına bakın.', open: 'GitHub Sürümlerini aç',
  },
}
const t = computed(() => STR[isTr.value ? 'tr' : 'en'])
const latest = computed(() => data[0])
const previous = computed(() => data.slice(1, 4))

function fmtDate(iso: string) {
  const d = new Date(iso)
  return isNaN(d.getTime()) ? '' : d.toLocaleDateString(isTr.value ? 'tr-TR' : 'en-US', { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' })
}
function fmtSize(bytes: number) {
  return bytes >= 1048576 ? `${(bytes / 1048576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`
}
</script>

<template>
  <div class="dl">
    <section v-if="latest" class="hero">
      <div class="meta">
        {{ t.latest }}
        <span v-if="latest.prerelease" class="badge">{{ t.alpha }}</span>
      </div>
      <div class="ver">Macro Grid {{ latest.version }}</div>
      <div class="sub">{{ fmtDate(latest.publishedAt) }} &middot; {{ t.os }} &middot; {{ fmtSize(latest.asset.size) }}</div>
      <a class="btn" :href="latest.asset.url">{{ t.download }}</a>
      <div class="small">
        <code>{{ latest.asset.name }}</code> &middot; <a :href="latest.notesUrl">{{ t.notes }}</a>
      </div>
    </section>

    <section v-else class="hero">
      <div class="ver">{{ t.none }}</div>
      <div class="sub">{{ t.see }}</div>
      <a class="btn" :href="RELEASES">{{ t.open }}</a>
    </section>

    <template v-if="previous.length">
      <h2 id="previous-versions" tabindex="-1">{{ t.previous }}</h2>
      <ul class="prev">
        <li v-for="r in previous" :key="r.tag">
          <span class="pv">{{ r.version }}<span v-if="r.prerelease" class="badge">{{ t.alpha }}</span></span>
          <span class="pd">{{ fmtDate(r.publishedAt) }}</span>
          <a :href="r.asset.url">{{ t.dl }}</a>
        </li>
      </ul>
    </template>

    <p class="older">{{ t.olderBefore }}<a :href="RELEASES">{{ t.olderLink }}</a>{{ t.olderAfter }}</p>
  </div>
</template>

<style scoped>
.hero {
  margin: 24px 0;
  padding: 28px 20px;
  text-align: center;
  border: 1px solid var(--vp-c-divider);
  border-radius: 12px;
  background: var(--vp-c-bg-soft);
}
.meta { color: var(--vp-c-text-2); font-size: 14px; }
.ver { margin-top: 4px; font-size: 26px; font-weight: 700; line-height: 1.3; color: var(--vp-c-text-1); }
.sub { margin-top: 4px; color: var(--vp-c-text-2); font-size: 14px; }
.btn {
  display: inline-block;
  margin-top: 18px;
  padding: 12px 28px;
  border-radius: 24px;
  background: var(--vp-button-brand-bg);
  color: var(--vp-button-brand-text) !important;
  font-weight: 600;
  text-decoration: none !important;
  transition: background-color 0.25s;
}
.btn:hover { background: var(--vp-button-brand-hover-bg); }
.small { margin-top: 12px; font-size: 13px; color: var(--vp-c-text-2); overflow-wrap: anywhere; }
.badge {
  margin-left: 6px;
  padding: 1px 8px;
  border-radius: 10px;
  background: var(--vp-c-brand-soft);
  color: var(--vp-c-brand-1);
  font-size: 12px;
  font-weight: 600;
  vertical-align: middle;
}
.prev { list-style: none; margin: 12px 0; padding: 0; }
.prev li {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px 16px;
  padding: 10px 0;
  border-bottom: 1px solid var(--vp-c-divider);
  margin: 0;
}
.pv { font-weight: 600; min-width: 90px; }
.pd { color: var(--vp-c-text-2); flex: 1; }
.older { margin-top: 16px; }
</style>
