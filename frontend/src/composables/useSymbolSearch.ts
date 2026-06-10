import type { SymbolOption } from '../data/symbols'

/** Subsequence ("fuzzy") match: are the chars of q present in order within text? */
function isSubsequence(text: string, q: string): boolean {
  let i = 0
  for (let j = 0; j < text.length && i < q.length; j++) {
    if (text[j] === q[i]) i++
  }
  return i === q.length
}

/**
 * Ranks a symbol universe against a query, "elastic"-style: exact symbol &gt;
 * symbol prefix &gt; symbol substring &gt; name substring &gt; fuzzy subsequence.
 * Pure and synchronous so it is trivially testable and instant on the client.
 */
export function searchSymbols(query: string, universe: SymbolOption[], limit = 8): SymbolOption[] {
  const q = query.trim().toUpperCase()
  if (q === '') return universe.slice(0, limit)

  const ranked: { option: SymbolOption; score: number }[] = []
  for (const option of universe) {
    const symbol = option.symbol.toUpperCase()
    const name = option.name.toUpperCase()

    let score = -1
    if (symbol === q) score = 100
    else if (symbol.startsWith(q)) score = 80 - (symbol.length - q.length)
    else if (symbol.includes(q)) score = 50
    else if (name.includes(q)) score = 30
    else if (isSubsequence(symbol, q)) score = 10

    if (score >= 0) ranked.push({ option, score })
  }

  ranked.sort((a, b) => b.score - a.score || a.option.symbol.localeCompare(b.option.symbol))
  return ranked.slice(0, limit).map((entry) => entry.option)
}

/** Merge the bundled universe with symbols already traded (deduped, uppercased). */
export function mergeUniverse(base: SymbolOption[], tradedSymbols: string[]): SymbolOption[] {
  const seen = new Set(base.map((o) => o.symbol.toUpperCase()))
  const merged = base.slice()
  for (const raw of tradedSymbols) {
    const symbol = raw.trim().toUpperCase()
    if (symbol !== '' && !seen.has(symbol)) {
      seen.add(symbol)
      merged.push({ symbol, name: 'Traded symbol' })
    }
  }
  return merged
}
