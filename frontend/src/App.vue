<script setup lang="ts">
import TradeBlotterView from './views/TradeBlotterView.vue'
import { storeToRefs } from 'pinia'
import { useTheme } from './composables/useTheme'
import { useTradeStore } from './stores/tradeStore'

const { theme, toggle } = useTheme()
const store = useTradeStore()
const { realtimeStatus } = storeToRefs(store)

const statusLabel = { connecting: 'Connecting…', connected: 'Live', disconnected: 'Offline' }
</script>

<template>
  <div class="app">
    <header class="app__header">
      <h1>Trade Blotter</h1>
      <div class="app__actions">
        <span
          class="live"
          :class="`live--${realtimeStatus}`"
          role="status"
          :title="`Hub connection: ${statusLabel[realtimeStatus]}`"
        >
          <span class="live__dot" aria-hidden="true"></span>
          {{ statusLabel[realtimeStatus] }}
        </span>
        <button type="button" class="action" @click="store.loadAll()">↻ Refresh all</button>
        <button
          type="button"
          class="action"
          :aria-pressed="theme === 'dark'"
          :title="theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'"
          @click="toggle"
        >
          {{ theme === 'dark' ? '☀️ Light' : '🌙 Dark' }}
        </button>
      </div>
    </header>
    <TradeBlotterView />
  </div>
</template>

<style scoped>
.app {
  max-width: 1200px;
  margin: 0 auto;
  padding: 1.5rem;
}
.app__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.5rem;
}
.app__header h1 {
  margin: 0;
  font-size: 1.5rem;
}
.app__actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.live {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.8rem;
  color: var(--muted);
}
.live__dot {
  width: 0.55rem;
  height: 0.55rem;
  border-radius: 50%;
  background: var(--muted);
}
.live--connected {
  color: var(--pos-long);
}
.live--connected .live__dot {
  background: var(--pos-long);
  box-shadow: 0 0 6px var(--pos-long);
}
.live--connecting {
  color: #d8a200;
}
.live--connecting .live__dot {
  background: #d8a200;
}
.live--disconnected {
  color: var(--pos-short);
}
.live--disconnected .live__dot {
  background: var(--pos-short);
}
.action {
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text);
  border-radius: 8px;
  padding: 0.4rem 0.8rem;
  font-weight: 600;
  cursor: pointer;
}
.action:hover {
  background: var(--surface-raised);
}
</style>
