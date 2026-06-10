<script setup lang="ts">
defineProps<{ side: string }>()
</script>

<template>
  <span class="side-badge" :class="side === 'Buy' ? 'side-badge--buy' : 'side-badge--sell'">
    {{ side }}
  </span>
</template>

<style scoped>
.side-badge {
  display: inline-block;
  min-width: 3.25rem;
  padding: 0.1rem 0.5rem;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 600;
  text-align: center;
  border: 1px solid transparent;
}
.side-badge--buy {
  color: var(--buy-fg);
  background: var(--buy-bg);
  border-color: var(--buy-border);
}
.side-badge--sell {
  color: var(--sell-fg);
  background: var(--sell-bg);
  border-color: var(--sell-border);
}
</style>
