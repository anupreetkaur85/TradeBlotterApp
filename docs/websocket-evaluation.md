# Real-Time Updates: SignalR Design & Implementation

A blotter is a shared, live view: when one trader submits a trade, every connected
client should see it immediately and positions should re-derive without a manual
refresh. This document records the real-time design adopted for the trade blotter.
It supersedes the frontend-only `BroadcastChannel` sync, which only synchronized
tabs within a single browser.

## Why SignalR (vs. SSE)

The blotter only needs **server → client** push (clients submit over REST, never
through the socket), so **Server-Sent Events** was a viable, lighter option.
**SignalR** was chosen for its batteries-included resilience (automatic reconnect,
transport fallback) and a clear scale-out path (a Redis / Azure SignalR backplane)
should multiple instances ever be needed.

## SignalR pitfalls we designed against

| Pitfall | How it's avoided here |
|---|---|
| **No backplane** — in-memory `AddSignalR()` only reaches clients on the same instance | Single-instance is fine for the assessment; the backplane is a documented scale path (below), not silently assumed. |
| **No reconnect reconciliation** — events emitted while a client is disconnected are lost | On reconnect (and after any re-establish) the client refetches (`loadAll`) to recover missed state. |
| **Broadcasting on the request path** — a hub/transient failure surfacing as a request error or latency | Notifications are enqueued to a bounded channel and published by a background loop, fully decoupled from `POST /trades`. |
| **Over-broadcast / leaking connection ids** | The hub is receive-only and broadcasts just the trade payload and a reconcile signal — no presence chatter or connection ids. |
| **Coupling business code to the transport** | The endpoint depends only on an `ITradeNotifier` abstraction; SignalR lives behind it. |

## Two key design choices

1. **Clean layering.** The endpoint/application layer depends only on an
   `ITradeNotifier` abstraction (`Application/Abstractions/Services/ITradeNotifier.cs`);
   the SignalR implementation (`Api/Hubs/SignalRTradeNotifier.cs`) is an
   infrastructure detail.
2. **Notifications never fail or delay the request.** `SignalRTradeNotifier`
   accepts committed changes through a bounded channel; its hosted background loop
   logs broadcast errors independently of `POST /trades`. Queue overflow is
   coalesced into a later `BlotterChanged` reconciliation event.

## Implementation

### Backend
- `Api/Hubs/BlotterHub.cs` — a receive-only hub at **`/hubs/blotter`** (clients
  never invoke it; trades are submitted over REST).
- `Application/Abstractions/Services/ITradeNotifier.cs` — non-blocking
  `TradeCreated(trade)` and `BlotterChanged()` enqueue operations.
- `Api/Hubs/SignalRTradeNotifier.cs` — bounded event channel and hosted publisher.
  `TradeCreated` carries the authoritative response; `BlotterChanged` requests a
  full reconciliation after seed mutations.
- `Program.cs` — registers one notifier instance as both `ITradeNotifier` and a
  hosted service, plus SignalR, credentialed CORS, and the hub route.
- `TradeEndpoints.CreateAsync` enqueues only newly created rows; idempotent
  replays do not re-broadcast.

### Frontend
- `api/realtime.ts` — SignalR client with `withAutomaticReconnect()`, initial
  connect retry (backend may not be up yet), and reconnect/close callbacks.
- `stores/tradeStore.ts` — `initRealtime()` connects and wires handlers:
  `TradeCreated` -> deduped prepend plus a coalesced position refresh;
  `BlotterChanged` and reconnect -> `loadAll()`. Trade snapshots preserve events
  received while REST requests are in flight. `realtimeStatus` drives a
  **● Live / Connecting… / Offline** header indicator.
- `BroadcastChannel` was removed — SignalR covers same-browser tabs *and* other
  browsers/machines/users.

## Verification

- Backend builds; backend unit tests, frontend type-check, frontend tests, and
  the production frontend build all pass.
- `POST /hubs/blotter/negotiate` returns 200 advertising the WebSockets transport.
- Two-**browser** (separate contexts) end-to-end: both show "Live"; a submit in one
  pushed the new trade row **and** position to the other with no manual reload —
  something `BroadcastChannel` could not do across separate browsers.

## Remaining work (future)
- **Redis backplane** (or Azure SignalR) for multi-instance scale-out — the current
  in-memory hub is single-instance only.
- **Hub authorization** once auth exists; consider **groups** (e.g. per symbol) if
  per-view filtering is needed.
- Optional **MessagePack** protocol and a connectivity-level hub health check.
