<script setup lang="ts">
import { onMounted } from 'vue'
import { useTradeStore } from '../stores/tradeStore'
import PositionsPanel from '../components/PositionsPanel.vue'
import TradeBlotter from '../components/TradeBlotter.vue'
import TradeEntryForm from '../components/TradeEntryForm.vue'

const store = useTradeStore()

onMounted(() => {
  store.initRealtime() // live updates from any client via SignalR
  void store.loadAll()
})
</script>

<template>
  <div class="layout">
    <aside class="layout__side">
      <div class="card">
        <TradeEntryForm />
      </div>
      <div class="card">
        <PositionsPanel />
      </div>
    </aside>
    <main class="layout__main">
      <div class="card">
        <TradeBlotter />
      </div>
    </main>
  </div>
</template>

<style scoped>
.layout {
  display: grid;
  grid-template-columns: minmax(280px, 340px) 1fr;
  gap: 1.5rem;
  align-items: start;
}
.layout__side {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}
.card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 1.25rem;
}
@media (max-width: 820px) {
  .layout {
    grid-template-columns: 1fr;
  }
}
</style>
