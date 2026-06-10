import { describe, expect, it } from 'vitest'
import { formatCurrency, formatSignedQuantity } from './format'

describe('format', () => {
  it('formats currency to two decimals', () => {
    expect(formatCurrency(18750)).toBe('$18,750.00')
    expect(formatCurrency(187.5)).toBe('$187.50')
  })

  it('formats net quantity with an explicit sign', () => {
    expect(formatSignedQuantity(150)).toBe('+150')
    expect(formatSignedQuantity(-50)).toBe('-50')
    expect(formatSignedQuantity(0)).toBe('0')
  })
})
