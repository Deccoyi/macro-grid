<script setup lang="ts">
import { withBase } from 'vitepress'
</script>

<template>
  <img class="deck" :src="withBase('/img/deck-phone.png')" alt="A Macro Grid deck on a phone" />
</template>

<style scoped>
.deck {
  display: block;
  max-height: 520px;
  width: auto;
  max-width: 100%;
  border-radius: 20px;
  border: 1px solid var(--vp-c-divider);
}
@media (max-width: 959px) {
  .deck { max-height: 380px; }
}
</style>
