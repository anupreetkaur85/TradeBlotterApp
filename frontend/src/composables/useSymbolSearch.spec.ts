import { describe, expect, it } from 'vitest'
import { mergeUniverse, searchSymbols } from './useSymbolSearch'
import type { SymbolOption } from '../data/symbols'

const universe: SymbolOption[] = [
  { symbol: 'AAPL', name: 'Apple Inc.' },
  { symbol: 'AMZN', name: 'Amazon.com, Inc.' },
  { symbol: 'GOOGL', name: 'Alphabet Inc. Class A' },
  { symbol: 'MSFT', name: 'Microsoft Corporation' },
]

describe('searchSymbols', () => {
  it('ranks an exact symbol match first', () => {
    expect(searchSymbols('AAPL', universe)[0].symbol).toBe('AAPL')
  })

  it('prefers prefix matches', () => {
    const result = searchSymbols('AM', universe)
    expect(result[0].symbol).toBe('AMZN')
  })

  it('matches on company name', () => {
    const result = searchSymbols('micro', universe)
    expect(result.map((o) => o.symbol)).toContain('MSFT')
  })

  it('supports fuzzy subsequence matches', () => {
    // "GGL" is a subsequence of GOOGL.
    expect(searchSymbols('ggl', universe).map((o) => o.symbol)).toContain('GOOGL')
  })

  it('returns the whole universe (capped) for an empty query', () => {
    expect(searchSymbols('', universe, 2)).toHaveLength(2)
  })
})

describe('mergeUniverse', () => {
  it('adds traded symbols not already present, deduped and uppercased', () => {
    const merged = mergeUniverse(universe, ['tsla', 'AAPL', 'tsla'])
    const symbols = merged.map((o) => o.symbol)
    expect(symbols).toContain('TSLA')
    expect(symbols.filter((s) => s === 'TSLA')).toHaveLength(1)
    expect(symbols.filter((s) => s === 'AAPL')).toHaveLength(1)
  })
})
