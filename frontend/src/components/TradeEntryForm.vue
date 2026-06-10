<script setup lang="ts">
import { ref } from 'vue'
import { useTradeForm } from '../composables/useTradeForm'
import InlineAlert from './InlineAlert.vue'
import SideToggle from './SideToggle.vue'
import SymbolAutocomplete from './SymbolAutocomplete.vue'

const { draft, fieldErrors, showSuccess, submit, canSubmit, submitting } = useTradeForm()
const symbolField = ref<InstanceType<typeof SymbolAutocomplete> | null>(null)

async function onSubmit(): Promise<void> {
  await submit()
  await symbolField.value?.focus()
}
</script>

<template>
  <form class="trade-form" novalidate @submit.prevent="onSubmit">
    <h2 class="trade-form__title">New trade</h2>

    <div class="field">
      <label for="symbol">Symbol</label>
      <SymbolAutocomplete
        ref="symbolField"
        v-model="draft.symbol"
        input-id="symbol"
        :invalid="Boolean(fieldErrors.symbol)"
        :describedby="fieldErrors.symbol ? 'symbol-error' : undefined"
      />
      <span v-if="fieldErrors.symbol" id="symbol-error" class="field__error">{{ fieldErrors.symbol }}</span>
    </div>

    <div class="field">
      <span class="field__label">Side</span>
      <SideToggle v-model="draft.side" />
    </div>

    <div class="field">
      <label for="quantity">Quantity</label>
      <input
        id="quantity"
        v-model="draft.quantity"
        type="number"
        min="1"
        step="1"
        inputmode="numeric"
        :aria-invalid="Boolean(fieldErrors.quantity)"
        :aria-describedby="fieldErrors.quantity ? 'quantity-error' : undefined"
      />
      <span v-if="fieldErrors.quantity" id="quantity-error" class="field__error">{{ fieldErrors.quantity }}</span>
    </div>

    <div class="field">
      <label for="price">Price</label>
      <input
        id="price"
        v-model="draft.price"
        type="number"
        min="0"
        step="0.0001"
        inputmode="decimal"
        :aria-invalid="Boolean(fieldErrors.price)"
        :aria-describedby="fieldErrors.price ? 'price-error' : undefined"
      />
      <span v-if="fieldErrors.price" id="price-error" class="field__error">{{ fieldErrors.price }}</span>
    </div>

    <button class="trade-form__submit" type="submit" :disabled="!canSubmit">
      {{ submitting ? 'Submitting…' : 'Submit trade' }}
    </button>

    <p class="visually-hidden" aria-live="polite">
      <span v-if="showSuccess">Trade submitted.</span>
    </p>
    <InlineAlert v-if="showSuccess" message="Trade submitted." tone="success" />
  </form>
</template>

<style scoped>
.trade-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
.trade-form__title {
  margin: 0;
  font-size: 1.1rem;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}
.field label,
.field__label {
  font-size: 0.85rem;
  font-weight: 600;
}
.field input {
  min-height: 2.5rem;
  padding: 0.4rem 0.6rem;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  color: var(--text);
  font: inherit;
}
.field input[aria-invalid='true'] {
  border-color: var(--sell-fg);
}
.field__error {
  color: var(--sell-fg);
  font-size: 0.8rem;
}
.trade-form__submit {
  min-height: 2.75rem;
  border: none;
  border-radius: 8px;
  background: var(--accent);
  color: #fff;
  font-weight: 600;
  cursor: pointer;
}
.trade-form__submit:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}
</style>
