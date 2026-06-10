// UI-facing domain types.

export type Side = 'Buy' | 'Sell'

export interface NewTradeInput {
  symbol: string
  side: Side
  quantity: number
  price: number
}

export type SortKey = 'timestamp' | 'symbol' | 'notional'
export type SortDirection = 'asc' | 'desc'
