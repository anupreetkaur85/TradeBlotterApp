<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useTradeStore } from '../stores/tradeStore'
import { formatCurrency, formatSignedQuantity } from '../utils/format'
import InlineAlert from './InlineAlert.vue'

const store = useTradeStore()
const { positions, positionsLoading, positionsError } = storeToRefs(store)
</script>

<template>
  <section class="positions" aria-labelledby="positions-title">
    <header class="positions__header">
      <h2 id="positions-title">Positions</h2>
      <button type="button" class="link" :disabled="positionsLoading" @click="store.loadPositions()">
        Refresh
      </button>
    </header>

    <InlineAlert v-if="positionsError" :message="positionsError" />
    <p v-else-if="positionsLoading" class="muted">Loading positions…</p>
    <p v-else-if="positions.length === 0" class="muted">No open positions.</p>

    <ul v-else class="positions__list">
      <li v-for="position in positions" :key="position.symbol" class="positions__row">
        <span class="positions__symbol">{{ position.symbol }}</span>
        <span
          class="positions__qty"
          :class="position.netQuantity >= 0 ? 'is-long' : 'is-short'"
        >
          {{ formatSignedQuantity(position.netQuantity) }}
          <small>{{ position.netQuantity >= 0 ? 'long' : 'short' }}</small>
        </span>
        <span class="positions__avg">{{ formatCurrency(position.averageCost) }}</span>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.positions__header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
}
.positions__header h2 {
  margin: 0 0 0.75rem;
  font-size: 1.1rem;
}
.link {
  background: none;
  border: none;
  color: var(--accent);
  cursor: pointer;
  font: inherit;
}
.link:disabled {
  opacity: 0.5;
  cursor: default;
}
.positions__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.positions__row {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 0.75rem;
  align-items: baseline;
  padding: 0.4rem 0.25rem;
  border-bottom: 1px solid var(--border);
}
.positions__symbol {
  font-weight: 600;
}
.positions__qty {
  font-variant-numeric: tabular-nums;
  text-align: right;
}
.positions__qty small {
  margin-left: 0.25rem;
  color: var(--muted);
}
.positions__qty.is-long {
  color: var(--pos-long);
}
.positions__qty.is-short {
  color: var(--pos-short);
}
.positions__avg {
  font-variant-numeric: tabular-nums;
  text-align: right;
}
.muted {
  color: var(--muted);
}
</style>
