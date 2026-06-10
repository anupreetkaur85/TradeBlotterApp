import { request } from './httpClient'
import type { PositionResponse, SymbolResponse, TradeResponse } from '../types/api'
import type { NewTradeInput } from '../types/trade'

/** Typed API client. Components never call fetch directly. */
export const tradeApi = {
  getTrades(signal?: AbortSignal): Promise<TradeResponse[]> {
    return request<TradeResponse[]>('/trades', { signal })
  },

  getPositions(signal?: AbortSignal): Promise<PositionResponse[]> {
    return request<PositionResponse[]>('/positions', { signal })
  },

  createTrade(input: NewTradeInput, idempotencyKey?: string): Promise<TradeResponse> {
    return request<TradeResponse>('/trades', {
      method: 'POST',
      body: input,
      headers: idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {},
    })
  },

  searchSymbols(query: string, limit = 8, signal?: AbortSignal): Promise<SymbolResponse[]> {
    const params = new URLSearchParams({ q: query, limit: String(limit) })
    return request<SymbolResponse[]>(`/symbols/search?${params.toString()}`, { signal })
  },
}
