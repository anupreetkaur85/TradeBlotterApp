// Wire-format DTOs returned by the Trade Blotter API (camelCase JSON).

export interface TradeResponse {
  tradeId: number
  symbol: string
  side: string
  quantity: number
  price: number
  notional: number
  timestamp: string
}

export interface PositionResponse {
  symbol: string
  netQuantity: number
  averageCost: number
}

export interface SymbolResponse {
  symbol: string
  name: string
}

/** RFC 7807 problem details / validation problem details. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
  errors?: Record<string, string[]>
}
