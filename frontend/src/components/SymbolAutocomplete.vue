<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { tradeApi } from '../api/tradeApi'
import { SYMBOL_UNIVERSE, type SymbolOption } from '../data/symbols'
import { mergeUniverse, searchSymbols } from '../composables/useSymbolSearch'
import { useTradeStore } from '../stores/tradeStore'

withDefaults(
  defineProps<{
    modelValue: string
    inputId?: string
    invalid?: boolean
    describedby?: string
  }>(),
  { inputId: 'symbol', invalid: false, describedby: undefined },
)

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const store = useTradeStore()
const { trades } = storeToRefs(store)

const input = ref<HTMLInputElement | null>(null)
const ghost = ref('') // full completed symbol currently shown (the added suffix is selected)
let typedText = '' // what the user actually typed (the prefix), without the ghost suffix
let debounceTimer: number | null = null
let requestSeq = 0

// Offline / server-error fallback: bundled tickers plus symbols already traded.
function fallbackSearch(query: string): SymbolOption[] {
  const universe = mergeUniverse(
    SYMBOL_UNIVERSE,
    trades.value.map((t) => t.symbol),
  )
  return searchSymbols(query, universe)
}

async function fetchCandidates(query: string): Promise<SymbolOption[]> {
  const seq = ++requestSeq
  try {
    const results = await tradeApi.searchSymbols(query, 8)
    if (seq !== requestSeq) return []
    return results.map((r) => ({ symbol: r.symbol, name: r.name }))
  } catch {
    if (seq !== requestSeq) return []
    return fallbackSearch(query)
  }
}

/** Inline ghost completion: extend the field to the top symbol-prefix match. */
async function complete(typed: string): Promise<void> {
  const el = input.value
  if (!el) return
  if (typed === '') {
    ghost.value = ''
    return
  }

  const candidates = await fetchCandidates(typed)
  if (typed !== typedText) return // a newer keystroke superseded this one

  const upper = typed.toUpperCase()
  const match = candidates.find(
    (c) => c.symbol.toUpperCase().startsWith(upper) && c.symbol.length > typed.length,
  )
  if (!match) {
    ghost.value = ''
    return
  }

  emit('update:modelValue', match.symbol)
  await nextTick() // let the controlled :value update, then apply our selection
  el.value = match.symbol
  el.setSelectionRange(typed.length, match.symbol.length)
  ghost.value = match.symbol
}

function scheduleComplete(typed: string): void {
  if (debounceTimer !== null) window.clearTimeout(debounceTimer)
  debounceTimer = window.setTimeout(() => void complete(typed), 80)
}

function onInput(event: Event): void {
  const inputEvent = event as InputEvent
  const el = event.target as HTMLInputElement
  typedText = el.value
  ghost.value = ''
  emit('update:modelValue', el.value)

  // Don't auto-complete while deleting, so backspace can erase through the ghost.
  if (inputEvent.inputType?.startsWith('delete')) return
  scheduleComplete(el.value)
}

function acceptGhost(el: HTMLInputElement): void {
  el.setSelectionRange(el.value.length, el.value.length)
  typedText = el.value
  ghost.value = ''
}

function onKeydown(event: KeyboardEvent): void {
  const el = event.target as HTMLInputElement
  if (ghost.value === '') return

  if (event.key === 'ArrowRight' || event.key === 'Tab' || event.key === 'Enter') {
    if (el.selectionStart !== el.selectionEnd) {
      acceptGhost(el) // Tab moves focus, Enter submits — both keep the completed value
      if (event.key === 'ArrowRight') event.preventDefault()
    }
  } else if (event.key === 'Escape') {
    el.value = typedText
    emit('update:modelValue', typedText)
    ghost.value = ''
  }
}

async function focus(): Promise<void> {
  await nextTick()
  input.value?.focus()
}
defineExpose({ focus })

onBeforeUnmount(() => {
  if (debounceTimer !== null) window.clearTimeout(debounceTimer)
})
</script>

<template>
  <div class="autocomplete">
    <input
      :id="inputId"
      ref="input"
      :value="modelValue"
      type="text"
      class="autocomplete__input"
      autocomplete="off"
      spellcheck="false"
      aria-autocomplete="inline"
      :aria-invalid="invalid"
      :aria-describedby="describedby"
      placeholder="Search symbol, e.g. AAPL"
      @input="onInput"
      @keydown="onKeydown"
    />
  </div>
</template>

<style scoped>
.autocomplete {
  position: relative;
}
.autocomplete__input {
  width: 100%;
  min-height: 2.5rem;
  padding: 0.4rem 0.6rem;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  color: var(--text);
  font: inherit;
}
.autocomplete__input[aria-invalid='true'] {
  border-color: var(--sell-fg);
}
</style>
