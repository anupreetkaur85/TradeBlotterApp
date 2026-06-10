import * as signalR from '@microsoft/signalr'
import type { TradeResponse } from '../types/api'

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
const HUB_URL = `${baseUrl}/hubs/blotter`

export type RealtimeStatus = 'connecting' | 'connected' | 'disconnected'

export interface RealtimeHandlers {
  onTradeCreated: (trade: TradeResponse) => void
  onBlotterChanged: () => void
  onReconnected: () => void
  onStatus: (status: RealtimeStatus) => void
}

let connection: signalR.HubConnection | null = null
let everConnected = false
let restartTimer: number | null = null

/**
 * Connects to the SignalR blotter hub and keeps the connection alive
 * indefinitely. Recovery is layered:
 *  - withAutomaticReconnect handles brief drops quickly (keeps the connection id),
 *  - onclose (fired when auto-reconnect gives up, e.g. a long backend restart)
 *    falls back to an unbounded retry loop.
 * After ANY successful re-establish we reconcile via onReconnected (refetch),
 * because events emitted during the gap are not replayed.
 */
export function connectRealtime(handlers: RealtimeHandlers): void {
  if (connection) return

  connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  connection.on('TradeCreated', (trade: TradeResponse) => handlers.onTradeCreated(trade))
  connection.on('BlotterChanged', () => handlers.onBlotterChanged())

  connection.onreconnecting(() => handlers.onStatus('connecting'))
  connection.onreconnected(() => {
    handlers.onStatus('connected')
    handlers.onReconnected()
  })
  connection.onclose(() => {
    handlers.onStatus('disconnected')
    scheduleStart(handlers) // auto-reconnect exhausted — keep trying ourselves
  })

  void start(handlers)
}

function scheduleStart(handlers: RealtimeHandlers): void {
  if (restartTimer !== null) return
  restartTimer = window.setTimeout(() => {
    restartTimer = null
    void start(handlers)
  }, 3000)
}

async function start(handlers: RealtimeHandlers): Promise<void> {
  if (!connection || connection.state !== signalR.HubConnectionState.Disconnected) return
  handlers.onStatus('connecting')
  try {
    await connection.start()
    handlers.onStatus('connected')
    if (everConnected) handlers.onReconnected() // recover anything missed while down
    everConnected = true
  } catch {
    handlers.onStatus('disconnected')
    scheduleStart(handlers) // backend not up yet (or just bounced) — retry
  }
}
