const currencyFormat = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

const quantityFormat = new Intl.NumberFormat('en-US')

export function formatCurrency(value: number): string {
  return currencyFormat.format(value)
}

export function formatQuantity(value: number): string {
  return quantityFormat.format(value)
}

/** Net quantity with an explicit sign so long/short reads at a glance. */
export function formatSignedQuantity(value: number): string {
  const magnitude = quantityFormat.format(Math.abs(value))
  if (value > 0) return `+${magnitude}`
  if (value < 0) return `-${magnitude}`
  return magnitude
}

export function formatTimestamp(iso: string): string {
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? iso : date.toLocaleString()
}
