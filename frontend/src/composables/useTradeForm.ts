import { computed, reactive, ref } from 'vue'
import { useTradeStore } from '../stores/tradeStore'
import type { NewTradeInput, Side } from '../types/trade'

interface FormDraft {
  symbol: string
  side: Side
  quantity: string
  price: string
}

/**
 * Owns the local form draft and client-side field errors. Server-backed state
 * stays in the store; only the draft lives here.
 */
export function useTradeForm() {
  const store = useTradeStore()

  const draft = reactive<FormDraft>({ symbol: '', side: 'Buy', quantity: '', price: '' })
  const fieldErrors = reactive<Record<string, string>>({})
  const showSuccess = ref(false)

  function clearErrors(): void {
    for (const key of Object.keys(fieldErrors)) delete fieldErrors[key]
  }

  function validate(): NewTradeInput | null {
    clearErrors()

    const symbol = draft.symbol.trim().toUpperCase()
    const quantity = Number(draft.quantity)
    const price = Number(draft.price)

    if (symbol === '') fieldErrors.symbol = 'Symbol is required.'
    if (draft.quantity === '' || !Number.isInteger(quantity) || quantity <= 0) {
      fieldErrors.quantity = 'Quantity must be a positive whole number.'
    }
    if (draft.price === '' || !Number.isFinite(price) || price <= 0) {
      fieldErrors.price = 'Price must be greater than zero.'
    }

    return Object.keys(fieldErrors).length === 0 ? { symbol, side: draft.side, quantity, price } : null
  }

  function applyServerErrors(): void {
    const errors = store.submitError?.errors
    if (!errors) return
    for (const [field, messages] of Object.entries(errors)) {
      if (messages.length > 0) fieldErrors[field.toLowerCase()] = messages[0]
    }
  }

  async function submit(): Promise<void> {
    showSuccess.value = false
    const input = validate()
    if (!input) return

    const ok = await store.submitTrade(input)
    if (ok) {
      // Reset the whole new-trade form; focus returns to symbol for the next entry.
      draft.symbol = ''
      draft.side = 'Buy'
      draft.quantity = ''
      draft.price = ''
      showSuccess.value = true
    } else {
      applyServerErrors()
    }
  }

  const canSubmit = computed(
    () => draft.symbol.trim() !== '' && draft.quantity !== '' && draft.price !== '' && !store.submitting,
  )

  return {
    draft,
    fieldErrors,
    showSuccess,
    submit,
    canSubmit,
    submitting: computed(() => store.submitting),
  }
}
