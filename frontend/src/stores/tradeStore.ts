import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { ApiError } from '../api/httpClient'
import { connectRealtime, type RealtimeStatus } from '../api/realtime'
import { tradeApi } from '../api/tradeApi'
import type { PositionResponse, ProblemDetails, TradeResponse } from '../types/api'
import type { NewTradeInput, SortDirection, SortKey } from '../types/trade'

function messageFrom(error: unknown): string {
  if (error instanceof Error) return error.message
  return 'Unexpected error.'
}

export const useTradeStore = defineStore('trades', () => {
  // Server-backed dashboard data.
  const trades = ref<TradeResponse[]>([])
  const positions = ref<PositionResponse[]>([])

  // Independent per-panel request state.
  const tradesLoading = ref(false)
  const positionsLoading = ref(false)
  const submitting = ref(false)

  const tradesError = ref<string | null>(null)
  const positionsError = ref<string | null>(null)
  const submitError = ref<ProblemDetails | null>(null)

  // Live (SignalR) hub connection status.
  const realtimeStatus = ref<RealtimeStatus>('connecting')
  const realtimeConnected = computed(() => realtimeStatus.value === 'connected')
  let realtimeStarted = false
  let tradeMutationVersion = 0
  let latestTradeLoad = 0
  const tradeArrivalVersions = new Map<number, number>()
  let positionsRefresh: Promise<void> | null = null
  let positionsRefreshQueued = false

  // Sort state (the canonical server order is newest-first; sorting copies).
  const sortKey = ref<SortKey>('timestamp')
  const sortDirection = ref<SortDirection>('desc')

  const sortedTrades = computed<TradeResponse[]>(() => {
    const direction = sortDirection.value === 'asc' ? 1 : -1
    const key = sortKey.value
    return trades.value.slice().sort((a, b) => {
      let result: number
      if (key === 'symbol') result = a.symbol.localeCompare(b.symbol)
      else if (key === 'notional') result = a.notional - b.notional
      else result = a.timestamp.localeCompare(b.timestamp)
      if (result === 0) result = a.tradeId - b.tradeId
      return result * direction
    })
  })

  async function loadTrades(): Promise<void> {
    const requestVersion = ++latestTradeLoad
    const mutationVersionAtStart = tradeMutationVersion
    tradesLoading.value = true
    tradesError.value = null
    try {
      const loaded = await tradeApi.getTrades()
      if (requestVersion !== latestTradeLoad) return

      const loadedIds = new Set<number>()
      for (const trade of loaded) loadedIds.add(trade.tradeId)

      const receivedDuringRequest: TradeResponse[] = []
      for (const trade of trades.value) {
        const arrivalVersion = tradeArrivalVersions.get(trade.tradeId) ?? 0
        if (arrivalVersion > mutationVersionAtStart && !loadedIds.has(trade.tradeId)) {
          receivedDuringRequest.push(trade)
        }
      }

      trades.value = [...receivedDuringRequest, ...loaded]
    } catch (error) {
      if (requestVersion !== latestTradeLoad) return
      tradesError.value = messageFrom(error)
    } finally {
      if (requestVersion === latestTradeLoad) {
        tradesLoading.value = false
      }
    }
  }

  function loadPositions(): Promise<void> {
    if (positionsRefresh) {
      positionsRefreshQueued = true
      return positionsRefresh
    }

    positionsRefresh = runPositionRefreshes()
    return positionsRefresh
  }

  function awaitCurrentPositionRefresh(): Promise<void> {
    return positionsRefresh ?? loadPositions()
  }

  async function runPositionRefreshes(): Promise<void> {
    positionsLoading.value = true
    try {
      do {
        positionsRefreshQueued = false
        positionsError.value = null
        try {
          positions.value = await tradeApi.getPositions()
        } catch (error) {
          positionsError.value = messageFrom(error)
        }
      } while (positionsRefreshQueued)
    } finally {
      positionsLoading.value = false
      positionsRefresh = null
    }
  }

  /** One failed endpoint must not suppress the other panel. */
  async function loadAll(): Promise<void> {
    await Promise.allSettled([loadTrades(), loadPositions()])
  }

  /** Prepend a pushed trade, deduped by id (the submitting tab already has it). */
  function receiveTrade(trade: TradeResponse): void {
    if (trades.value.some((t) => t.tradeId === trade.tradeId)) return
    tradeMutationVersion++
    tradeArrivalVersions.set(trade.tradeId, tradeMutationVersion)
    trades.value = [trade, ...trades.value]
    void loadPositions()
  }

  /**
   * Connect to the live (SignalR) feed. Idempotent — call once on mount. Pushed
   * trades from any client (other tabs, browsers, users) land here in real time;
   * on reconnect we refetch to recover events missed during the gap.
   */
  function initRealtime(): void {
    if (realtimeStarted) return
    realtimeStarted = true
    connectRealtime({
      onTradeCreated: (trade) => receiveTrade(trade),
      onBlotterChanged: () => void loadAll(),
      onReconnected: () => void loadAll(),
      onStatus: (status) => {
        realtimeStatus.value = status
      },
    })
  }

  /**
   * Submit a trade. On success the authoritative server row is prepended and
   * positions are refreshed; a failed position refresh does NOT fail the submit.
   * The server also broadcasts the trade to every live client (including this
   * one — receiveTrade dedupes it).
   */
  async function submitTrade(input: NewTradeInput): Promise<boolean> {
    submitting.value = true
    submitError.value = null
    try {
      const created = await tradeApi.createTrade(input)
      receiveTrade(created)
      await awaitCurrentPositionRefresh()
      return true
    } catch (error) {
      submitError.value =
        error instanceof ApiError && error.problem ? error.problem : { title: messageFrom(error) }
      return false
    } finally {
      submitting.value = false
    }
  }

  function setSort(key: SortKey): void {
    if (sortKey.value === key) {
      sortDirection.value = sortDirection.value === 'asc' ? 'desc' : 'asc'
    } else {
      sortKey.value = key
      sortDirection.value = key === 'symbol' ? 'asc' : 'desc'
    }
  }

  return {
    trades,
    positions,
    tradesLoading,
    positionsLoading,
    submitting,
    tradesError,
    positionsError,
    submitError,
    realtimeStatus,
    realtimeConnected,
    sortKey,
    sortDirection,
    sortedTrades,
    loadTrades,
    loadPositions,
    loadAll,
    receiveTrade,
    initRealtime,
    submitTrade,
    setSort,
  }
})
