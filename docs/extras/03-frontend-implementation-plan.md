# Frontend Implementation Plan

## Technology baseline

- Vue 3 with Composition API and `<script setup lang="ts">`
- TypeScript
- Vite
- Pinia
- Native `fetch` behind a typed API client
- Vitest
- Vue Test Utils
- ESLint and Prettier

A component library is optional. For a small assessment, scoped CSS or a small
utility framework keeps dependencies and visual inconsistency low.

## Proposed frontend structure

```text
frontend/src/
  api/
    httpClient.ts
    tradeApi.ts
  components/
    TradeEntryForm.vue
    TradeBlotter.vue
    PositionsPanel.vue
    InlineAlert.vue
    LoadingState.vue
  stores/
    tradeStore.ts
  types/
    api.ts
    trade.ts
  views/
    TradeBlotterView.vue
  App.vue
  main.ts
```

## Type contracts

Define explicit frontend types that match the API:

```text
TradeSide = 'Buy' | 'Sell'
CreateTradeRequest
Trade
Position
ProblemDetails
```

Treat decimal values from the API consistently. For this assessment, JSON
numbers are acceptable for display and form submission, while the backend
remains authoritative for decimal arithmetic. Use `Intl.NumberFormat` for
price/notional display and enforce safe numeric input bounds.

## Pinia store

State:

- `trades: Trade[]`
- `positions: Position[]`
- `isLoading: boolean`
- `isSubmitting: boolean`
- `loadError: string | null`
- `submitError: ProblemDetails | null`
- `sortKey`
- `sortDirection`

Actions:

- `initialize()`: fetch trades and positions in parallel.
- `submitTrade(request)`: submit, prepend returned trade, refresh positions.
- `refreshTrades()`
- `refreshPositions()`
- `setSort(key)`

Getters:

- `sortedTrades`
- optionally `hasTrades` and `hasPositions`

Sorting is presentation state. Keep the canonical store array in server
newest-first order and return a sorted copy for the table. For large data sets,
move sorting and pagination to the API.

On position refresh failure after a successful trade insert:

- Keep the submitted trade visible.
- Show a non-destructive error for the positions panel.
- Offer/retry position refresh rather than pretending the trade failed.

Representative store flow:

```ts
async function initialize(): Promise<void> {
  isLoading.value = true
  const [tradesResult, positionsResult] = await Promise.allSettled([
    tradeApi.getTrades(),
    tradeApi.getPositions(),
  ])

  if (tradesResult.status === 'fulfilled') {
    trades.value = tradesResult.value
  } else {
    tradesError.value = toMessage(tradesResult.reason)
  }

  if (positionsResult.status === 'fulfilled') {
    positions.value = positionsResult.value
  } else {
    positionsError.value = toMessage(positionsResult.reason)
  }

  isLoading.value = false
}

async function submitTrade(input: CreateTradeRequest): Promise<Trade> {
  isSubmitting.value = true
  submitError.value = null

  try {
    const created = await tradeApi.createTrade(input)
    trades.value.unshift(created)

    try {
      positions.value = await tradeApi.getPositions()
      positionsError.value = null
    } catch (error) {
      positionsError.value = toMessage(error)
    }

    return created
  } catch (error) {
    submitError.value = toProblemDetails(error)
    throw error
  } finally {
    isSubmitting.value = false
  }
}
```

## Trade entry form

Fields:

- Symbol text input
- Side segmented control, radio group, or select
- Quantity numeric input
- Price numeric input
- Submit button

Validation:

- Normalize symbol to uppercase on blur or submit.
- Require every field.
- Enforce allowed symbol characters and maximum length.
- Quantity must be a positive integer within the backend limit.
- Price must be positive with at most four decimal places.
- Display field-level messages and an API summary when applicable.

Submission behavior:

1. Validate locally.
2. Set submitting state and disable repeated submission.
3. Await the Pinia action.
4. On success, clear quantity and price; retain symbol/side only if that feels
   efficient for repeated trader entry.
5. Move focus to the first useful field.
6. Announce success or failure through an accessible live region.

Do not use optimistic fake IDs or timestamps. The requirement for immediate
updates is satisfied by inserting the fast server response without reloading
the page.

Keep draft values and field errors in `useTradeForm`. A successful submission
clears quantity and price, retains or clears symbol according to the final UX
choice, and refocuses the symbol field. Server-side field errors are mapped
back to matching controls with `aria-describedby`.

## Blotter table

Columns:

- Timestamp
- Symbol
- Side
- Quantity
- Price
- Notional

Behavior:

- Default newest first.
- Make timestamp and/or symbol headers sortable buttons.
- Indicate active sort and direction with text/ARIA state, not color alone.
- Render buy as a restrained green badge/accent and sell as red.
- Use tabular numerals and right-align numeric columns.
- Keep symbol prominent and uppercase.
- Format timestamps in the browser's local timezone while retaining UTC data.
- Format price and notional with `Intl.NumberFormat`.
- Add an empty state instead of an empty table body.

Use `font-variant-numeric: tabular-nums` and right-align quantity, price, and
notional. Implement sortable headers as buttons and set `aria-sort` on the
active column.

For narrow screens, place the table in an accessible horizontal scroll
container. Do not hide required columns.

## Positions panel

Display:

- Symbol
- Net quantity
- Average cost

Visual treatment:

- Positive/long quantity: green accent and `+` sign.
- Negative/short quantity: red accent.
- Never rely on color alone.
- Empty state: "No open positions."

Positions come from the API, not a second frontend calculator. This avoids
duplicating domain rules and ensures a refresh reproduces the same state.

## Page layout

Desktop:

- Header and short context line.
- Trade form in a compact top or left card.
- Positions in a secondary card.
- Blotter gets the largest area.

Mobile:

- Stack form, positions, then blotter.
- Keep labels visible and tap targets at least 44px high.
- Avoid modal entry for the core workflow.

Recommended component boundary:

```text
TradeBlotterView
  TradeEntryForm
    SideToggle
  PositionsPanel
  TradeBlotter
    SideBadge
```

`SideBadge` owns the shared buy/sell visual language. Color is accompanied by
the visible Buy/Sell text. `SideToggle` may reuse the same tokens without
coupling the form control to a table-specific component.

## API client

- Base URL from `VITE_API_BASE_URL`.
- One helper handles JSON parsing, non-2xx responses, and ProblemDetails.
- Pass `AbortSignal` where useful.
- Keep endpoint-specific functions in `tradeApi.ts`.
- Do not scatter raw `fetch` calls across components/stores.

Development can use a Vite proxy to avoid local CORS friction, while the API
still has explicit CORS configuration for standalone runs.

## Frontend tests

### Store tests

- Initialization loads both resources.
- Successful submission prepends returned trade and refreshes positions.
- Failed submission preserves form/store state and exposes API error.
- Sort toggles direction and does not mutate canonical order.

### Component tests

- Form prevents empty/non-positive values.
- Form emits/submits a normalized valid request.
- Submit button disables while pending.
- Blotter calculates and formats notional.
- Sort header changes row order and ARIA state.
- Buy/sell presentation includes readable text.
- Positions render long, short, and empty states.

### Optional browser smoke test

Use Playwright only if time allows:

1. Open a clean/seeded application.
2. Submit a trade.
3. Verify it appears in the blotter.
4. Verify the corresponding position updates.

## Frontend implementation checklist

- [ ] Scaffold Vite Vue TypeScript application.
- [ ] Add Pinia, Vitest, Vue Test Utils, linting, and formatting.
- [ ] Define API/domain TypeScript contracts.
- [ ] Implement typed API client and ProblemDetails parsing.
- [ ] Implement Pinia store and store tests.
- [ ] Implement trade form and validation tests.
- [ ] Implement sortable blotter and tests.
- [ ] Implement positions panel and tests.
- [ ] Add responsive/accessibility styles.
- [ ] Add loading, empty, and error states.
- [ ] Verify production build and local API integration.
