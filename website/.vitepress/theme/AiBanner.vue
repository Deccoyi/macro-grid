<template>
  <div class="ai-banner" role="note">
    <strong>Alpha software, written entirely by AI.</strong>
    <span class="long">
      Nothing has been reviewed line by line by a human or security-audited. No warranty, no liability: you use it at your own risk.
    </span>
    <a href="https://deccoyi.github.io/macro-grid/guide/security">Read more</a>
  </div>
</template>

<style scoped>
.ai-banner {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: var(--vp-z-index-layout-top);
  height: var(--vp-layout-top-height);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  padding: 0 16px;
  background: var(--vp-button-brand-bg);
  color: var(--vp-button-brand-text);
  font-size: 13px;
  line-height: 1.3;
  text-align: center;
}
.ai-banner strong,
.ai-banner a { white-space: nowrap; }
.long {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ai-banner a {
  color: inherit;
  font-weight: 700;
  text-decoration: underline;
}
@media (max-width: 720px) {
  .long { display: none; }
}
</style>
