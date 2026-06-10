<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useTradeStore } from '../stores/tradeStore'
import type { SortKey } from '../types/trade'
import { formatCurrency, formatQuantity, formatTimestamp } from '../utils/format'
import InlineAlert from './InlineAlert.vue'
import SideBadge from './SideBadge.vue'

const store = useTradeStore()
const { sortedTrades, tradesLoading, tradesError, sortKey, sortDirection } = storeToRefs(store)

const sortableColumns: { key: SortKey; label: string }[] = [
  { key: 'timestamp', label: 'Time' },
  { key: 'symbol', label: 'Symbol' },
  { key: 'notional', label: 'Notional' },
]

function ariaSort(key: SortKey): 'ascending' | 'descending' | 'none' {
  if (sortKey.value !== key) return 'none'
  return sortDirection.value === 'asc' ? 'ascending' : 'descending'
}
</script>

<template>
  <section class="blotter" aria-labelledby="blotter-title">
    <header class="blotter__header">
      <h2 id="blotter-title">Blotter</h2>
      <button type="button" class="link" :disabled="tradesLoading" @click="store.loadTrades()">
        {{ tradesLoading ? 'Refreshing…' : 'Refresh' }}
      </button>
    </header>

    <InlineAlert v-if="tradesError" :message="tradesError" />
    <p v-else-if="tradesLoading" class="muted">Loading trades…</p>
    <p v-else-if="sortedTrades.length === 0" class="muted">No trades yet. Submit one to get started.</p>

    <div v-else class="table-scroll">
      <table class="blotter-table">
        <thead>
          <tr>
            <th
              v-for="col in sortableColumns"
              :key="col.key"
              :aria-sort="ariaSort(col.key)"
              :class="{ numeric: col.key === 'notional' }"
            >
              <button type="button" class="sort-header" @click="store.setSort(col.key)">
                {{ col.label }}
                <span aria-hidden="true">{{ sortKey === col.key ? (sortDirection === 'asc' ? '▲' : '▼') : '' }}</span>
              </button>
            </th>
            <th>Side</th>
            <th class="numeric">Quantity</th>
            <th class="numeric">Price</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="trade in sortedTrades" :key="trade.tradeId">
            <td>{{ formatTimestamp(trade.timestamp) }}</td>
            <td class="symbol">{{ trade.symbol }}</td>
            <td class="numeric">{{ formatCurrency(trade.notional) }}</td>
            <td><SideBadge :side="trade.side" /></td>
            <td class="numeric">{{ formatQuantity(trade.quantity) }}</td>
            <td class="numeric">{{ formatCurrency(trade.price) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.blotter__header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  margin-bottom: 0.75rem;
}
.blotter__header h2 {
  margin: 0;
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
.table-scroll {
  overflow-x: auto;
}
.blotter-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}
.blotter-table th,
.blotter-table td {
  padding: 0.5rem 0.75rem;
  border-bottom: 1px solid var(--border);
  text-align: left;
  white-space: nowrap;
}
.blotter-table .numeric {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
.blotter-table .symbol {
  font-weight: 600;
}
.sort-header {
  background: none;
  border: none;
  padding: 0;
  font: inherit;
  font-weight: 700;
  cursor: pointer;
  display: inline-flex;
  gap: 0.3rem;
  align-items: center;
}
.muted {
  color: var(--muted);
}
</style>
