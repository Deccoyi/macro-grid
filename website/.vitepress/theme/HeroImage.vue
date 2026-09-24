<script setup lang="ts">
import { computed } from 'vue'
import { useData, withBase } from 'vitepress'

// Home page hero picture: `heroImage: { src, alt }` in the page frontmatter, or the logo.
const { frontmatter } = useData()
const custom = computed(() => frontmatter.value.heroImage as { src: string; alt?: string } | undefined)
</script>

<template>
  <img v-if="custom" class="shot" :src="withBase(custom.src)" :alt="custom.alt ?? ''" />
  <img v-else class="logo" :src="withBase('/logo.png')" alt="Macro Grid" />
</template>

<style scoped>
.shot {
  display: block;
  max-height: 520px;
  width: auto;
  max-width: 100%;
  border-radius: 20px;
  border: 1px solid var(--vp-c-divider);
}
.logo {
  display: block;
  height: 200px;
  width: auto;
  max-width: 100%;
  border-radius: 40px;
}
@media (max-width: 959px) {
  .shot { max-height: 380px; }
  .logo { height: 120px; border-radius: 24px; }
}
</style>
