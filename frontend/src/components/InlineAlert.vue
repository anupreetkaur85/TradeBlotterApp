<script setup lang="ts">
withDefaults(defineProps<{ message: string; tone?: 'error' | 'info' | 'success' }>(), {
  tone: 'error',
})
</script>

<template>
  <p class="inline-alert" :class="`inline-alert--${tone}`" role="alert">
    {{ message }}
  </p>
</template>

<style scoped>
.inline-alert {
  margin: 0;
  padding: 0.5rem 0.75rem;
  border-radius: 8px;
  font-size: 0.875rem;
}
.inline-alert--error {
  color: var(--err-fg);
  background: var(--err-bg);
}
.inline-alert--info {
  color: var(--info-fg);
  background: var(--info-bg);
}
.inline-alert--success {
  color: var(--ok-fg);
  background: var(--ok-bg);
}
</style>
