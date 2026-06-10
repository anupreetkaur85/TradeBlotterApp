import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { tradeApi } from '../api/tradeApi'
import type { TradeResponse } from '../types/api'
import { useTradeStore } from './tradeStore'

const realtimeMock = vi.hoisted(() => ({
  handlers: null as null | {
    onTradeCreated: (trade: TradeResponse) => void
    onBlotterChanged: () => void
    onReconnected: () => void
    onStatus: (status: 'connecting' | 'connected' | 'disconnected') => void
  },
}))

vi.mock('../api/tradeApi', () => ({
  tradeApi: {
    getTrades: vi.fn(),
    getPositions: vi.fn(),
    createTrade: vi.fn(),
  },
}))

vi.mock('../api/realtime', () => ({
  connectRealtime: vi.fn((handlers) => {
    realtimeMock.handlers = handlers
  }),
}))

const mockedApi = vi.mocked(tradeApi)

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise
    reject = rejectPromise
  })
  return { promise, resolve, reject }
}

function trade(partial: Partial<TradeResponse> & { tradeId: number }): TradeResponse {
  return {
    tradeId: partial.tradeId,
    symbol: partial.symbol ?? 'AAPL',
    side: partial.side ?? 'Buy',
    quantity: partial.quantity ?? 100,
    price: partial.price ?? 10,
    notional: partial.notional ?? 1000,
    timestamp: partial.timestamp ?? '2026-01-01T00:00:00Z',
  }
}

describe('tradeStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    realtimeMock.handlers = null
    mockedApi.getTrades.mockResolvedValue([])
    mockedApi.getPositions.mockResolvedValue([])
  })

  it('loadAll populates trades and positions', async () => {
    mockedApi.getTrades.mockResolvedValue([trade({ tradeId: 1 })])
    mockedApi.getPositions.mockResolvedValue([{ symbol: 'AAPL', netQuantity: 100, averageCost: 10 }])

    const store = useTradeStore()
    await store.loadAll()

    expect(store.trades).toHaveLength(1)
    expect(store.positions).toHaveLength(1)
  })

  it('submitTrade prepends the created trade and refreshes positions', async () => {
    mockedApi.createTrade.mockResolvedValue(trade({ tradeId: 7, symbol: 'MSFT' }))
    mockedApi.getPositions.mockResolvedValue([{ symbol: 'MSFT', netQuantity: 10, averageCost: 5 }])

    const store = useTradeStore()
    store.trades = [trade({ tradeId: 1 })]

    const ok = await store.submitTrade({ symbol: 'MSFT', side: 'Buy', quantity: 10, price: 5 })

    expect(ok).toBe(true)
    expect(store.trades[0].tradeId).toBe(7)
    expect(mockedApi.getPositions).toHaveBeenCalledOnce()
  })

  it('keeps submit successful even when the position refresh fails', async () => {
    mockedApi.createTrade.mockResolvedValue(trade({ tradeId: 9 }))
    mockedApi.getPositions.mockRejectedValue(new Error('positions down'))

    const store = useTradeStore()
    const ok = await store.submitTrade({ symbol: 'AAPL', side: 'Buy', quantity: 1, price: 1 })

    expect(ok).toBe(true)
    expect(store.positionsError).not.toBeNull()
  })

  it('receiveTrade prepends a pushed trade but dedupes by id', () => {
    const store = useTradeStore()
    store.trades = [trade({ tradeId: 1 })]

    store.receiveTrade(trade({ tradeId: 2, symbol: 'MSFT' }))
    expect(store.trades.map((t) => t.tradeId)).toEqual([2, 1])

    store.receiveTrade(trade({ tradeId: 2, symbol: 'MSFT' })) // duplicate push (e.g. own broadcast)
    expect(store.trades).toHaveLength(2)
  })

  it('retains a pushed trade received while a trade snapshot is loading', async () => {
    const pending = deferred<TradeResponse[]>()
    mockedApi.getTrades.mockReturnValue(pending.promise)

    const store = useTradeStore()
    const loading = store.loadTrades()
    store.receiveTrade(trade({ tradeId: 2, symbol: 'MSFT' }))

    pending.resolve([trade({ tradeId: 1 })])
    await loading

    expect(store.trades.map((item) => item.tradeId)).toEqual([2, 1])
  })

  it('discards a superseded trade snapshot response', async () => {
    const first = deferred<TradeResponse[]>()
    const second = deferred<TradeResponse[]>()
    mockedApi.getTrades
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)

    const store = useTradeStore()
    const firstLoad = store.loadTrades()
    const secondLoad = store.loadTrades()

    second.resolve([trade({ tradeId: 2 })])
    await secondLoad
    first.resolve([trade({ tradeId: 1 })])
    await firstLoad

    expect(store.trades.map((item) => item.tradeId)).toEqual([2])
  })

  it('coalesces concurrent position refreshes into one active request and one follow-up', async () => {
    const first = deferred<Awaited<ReturnType<typeof tradeApi.getPositions>>>()
    const second = deferred<Awaited<ReturnType<typeof tradeApi.getPositions>>>()
    mockedApi.getPositions
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)

    const store = useTradeStore()
    const one = store.loadPositions()
    const two = store.loadPositions()
    const three = store.loadPositions()

    expect(mockedApi.getPositions).toHaveBeenCalledOnce()

    first.resolve([{ symbol: 'AAPL', netQuantity: 1, averageCost: 10 }])
    await vi.waitFor(() => expect(mockedApi.getPositions).toHaveBeenCalledTimes(2))
    second.resolve([{ symbol: 'AAPL', netQuantity: 2, averageCost: 11 }])
    await Promise.all([one, two, three])

    expect(store.positions[0].netQuantity).toBe(2)
    expect(mockedApi.getPositions).toHaveBeenCalledTimes(2)
  })

  it('fully reconciles when the server reports a non-trade blotter change', async () => {
    mockedApi.getTrades.mockResolvedValue([trade({ tradeId: 5 })])
    mockedApi.getPositions.mockResolvedValue([
      { symbol: 'AAPL', netQuantity: 5, averageCost: 10 },
    ])

    const store = useTradeStore()
    store.initRealtime()
    realtimeMock.handlers!.onBlotterChanged()

    await vi.waitFor(() => expect(store.trades).toHaveLength(1))
    expect(store.positions).toHaveLength(1)
  })

  it('sortedTrades returns a sorted copy without mutating canonical order', () => {
    const store = useTradeStore()
    store.trades = [trade({ tradeId: 1, symbol: 'BBB' }), trade({ tradeId: 2, symbol: 'AAA' })]

    store.setSort('symbol') // ascending

    expect(store.sortedTrades.map((t) => t.symbol)).toEqual(['AAA', 'BBB'])
    expect(store.trades.map((t) => t.symbol)).toEqual(['BBB', 'AAA'])
  })
})
