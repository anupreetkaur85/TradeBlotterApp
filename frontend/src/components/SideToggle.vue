<script setup lang="ts">
import type { Side } from '../types/trade'

const props = defineProps<{ modelValue: Side }>()
const emit = defineEmits<{ 'update:modelValue': [value: Side] }>()

function select(side: Side): void {
  emit('update:modelValue', side)
}
</script>

<template>
  <div class="side-toggle" role="group" aria-label="Side">
    <button
      type="button"
      class="side-toggle__btn side-toggle__btn--buy"
      :class="{ active: props.modelValue === 'Buy' }"
      :aria-pressed="props.modelValue === 'Buy'"
      @click="select('Buy')"
    >
      Buy
    </button>
    <button
      type="button"
      class="side-toggle__btn side-toggle__btn--sell"
      :class="{ active: props.modelValue === 'Sell' }"
      :aria-pressed="props.modelValue === 'Sell'"
      @click="select('Sell')"
    >
      Sell
    </button>
  </div>
</template>

<style scoped>
.side-toggle {
  display: inline-flex;
  gap: 0.5rem;
}
.side-toggle__btn {
  min-width: 4.5rem;
  min-height: 2.5rem;
  padding: 0.4rem 0.9rem;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--surface);
  font-weight: 600;
  cursor: pointer;
}
.side-toggle__btn {
  color: var(--text);
}
.side-toggle__btn--buy.active {
  color: var(--buy-fg);
  background: var(--buy-bg);
  border-color: var(--buy-border);
}
.side-toggle__btn--sell.active {
  color: var(--sell-fg);
  background: var(--sell-bg);
  border-color: var(--sell-border);
}
</style>
