<script setup lang="ts">
const tiles = [
  { t: 'CPU 23%', c: '#2b2e33', f: '#e6e7ea', i: '▮▮▯▯' },
  { t: 'Scene: Game', c: '#d97706', f: '#1c1e22', i: '▶' },
  { t: 'LIVE 01:24:07', c: '#c0392b', f: '#ffffff', i: '●', blink: true },
  { t: 'Mute', c: '#3f9142', f: '#ffffff', i: '♪' },
  { t: 'Copy', c: '#2b2e33', f: '#e6e7ea', i: 'Ctrl+C' },
  { t: 'Paste', c: '#2b2e33', f: '#e6e7ea', i: 'Ctrl+V' },
]
</script>

<template>
  <div class="mock" aria-hidden="true">
    <div class="phone">
      <div class="notch"></div>
      <div class="grid">
        <div v-for="x in tiles" :key="x.t" class="tile" :class="{ blink: x.blink }"
             :style="{ background: x.c, color: x.f }">
          <span class="ic">{{ x.i }}</span>
          <span class="tx">{{ x.t }}</span>
        </div>
        <div class="tile slider"><div class="track"><div class="fill"></div></div><span class="tx">Volume 64%</span></div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.mock { display: flex; justify-content: center; padding: 8px 0; }
.phone { width: 260px; padding: 14px 12px 16px; border-radius: 34px; background: #16171a;
  border: 2px solid #46494f; box-shadow: 0 20px 50px rgba(0,0,0,.35); }
.notch { width: 70px; height: 6px; border-radius: 6px; background: #35383e; margin: 0 auto 12px; }
.grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 8px; }
.tile { border-radius: 12px; min-height: 76px; display: flex; flex-direction: column;
  align-items: center; justify-content: center; gap: 4px; font-size: 12px; font-weight: 600; padding: 6px; text-align: center; }
.ic { font-size: 17px; line-height: 1; }
.slider { grid-column: span 2; background: #2b2e33; color: #e6e7ea; min-height: 64px; }
.track { width: 86%; height: 8px; border-radius: 8px; background: #46494f; overflow: hidden; }
.fill { width: 64%; height: 100%; background: #d97706; }
.blink { animation: mg-blink 1.2s ease-in-out infinite; }
@keyframes mg-blink { 50% { opacity: .55; } }
@media (prefers-reduced-motion: reduce) { .blink { animation: none; } }
</style>
