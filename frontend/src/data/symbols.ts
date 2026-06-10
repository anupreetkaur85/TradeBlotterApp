export interface SymbolOption {
  symbol: string
  name: string
}

/**
 * A small bundled universe of common tickers for the autocomplete. This is the
 * client-side stand-in for a symbol search service; see useSymbolSearch for how
 * traded symbols are merged in. Swap this for a backend /symbols/search endpoint
 * (optionally Elasticsearch-backed) when the universe grows.
 */
export const SYMBOL_UNIVERSE: SymbolOption[] = [
  { symbol: 'AAPL', name: 'Apple Inc.' },
  { symbol: 'MSFT', name: 'Microsoft Corporation' },
  { symbol: 'GOOGL', name: 'Alphabet Inc. Class A' },
  { symbol: 'GOOG', name: 'Alphabet Inc. Class C' },
  { symbol: 'AMZN', name: 'Amazon.com, Inc.' },
  { symbol: 'NVDA', name: 'NVIDIA Corporation' },
  { symbol: 'META', name: 'Meta Platforms, Inc.' },
  { symbol: 'TSLA', name: 'Tesla, Inc.' },
  { symbol: 'BRK.B', name: 'Berkshire Hathaway Inc. Class B' },
  { symbol: 'JPM', name: 'JPMorgan Chase & Co.' },
  { symbol: 'V', name: 'Visa Inc.' },
  { symbol: 'MA', name: 'Mastercard Incorporated' },
  { symbol: 'UNH', name: 'UnitedHealth Group Incorporated' },
  { symbol: 'HD', name: 'The Home Depot, Inc.' },
  { symbol: 'PG', name: 'The Procter & Gamble Company' },
  { symbol: 'JNJ', name: 'Johnson & Johnson' },
  { symbol: 'XOM', name: 'Exxon Mobil Corporation' },
  { symbol: 'CVX', name: 'Chevron Corporation' },
  { symbol: 'KO', name: 'The Coca-Cola Company' },
  { symbol: 'PEP', name: 'PepsiCo, Inc.' },
  { symbol: 'COST', name: 'Costco Wholesale Corporation' },
  { symbol: 'WMT', name: 'Walmart Inc.' },
  { symbol: 'DIS', name: 'The Walt Disney Company' },
  { symbol: 'NFLX', name: 'Netflix, Inc.' },
  { symbol: 'ADBE', name: 'Adobe Inc.' },
  { symbol: 'CRM', name: 'Salesforce, Inc.' },
  { symbol: 'INTC', name: 'Intel Corporation' },
  { symbol: 'AMD', name: 'Advanced Micro Devices, Inc.' },
  { symbol: 'ORCL', name: 'Oracle Corporation' },
  { symbol: 'CSCO', name: 'Cisco Systems, Inc.' },
  { symbol: 'BAC', name: 'Bank of America Corporation' },
  { symbol: 'WFC', name: 'Wells Fargo & Company' },
  { symbol: 'GS', name: 'The Goldman Sachs Group, Inc.' },
  { symbol: 'MS', name: 'Morgan Stanley' },
  { symbol: 'BA', name: 'The Boeing Company' },
  { symbol: 'PFE', name: 'Pfizer Inc.' },
  { symbol: 'T', name: 'AT&T Inc.' },
  { symbol: 'VZ', name: 'Verizon Communications Inc.' },
  { symbol: 'NKE', name: 'NIKE, Inc.' },
  { symbol: 'PYPL', name: 'PayPal Holdings, Inc.' },
]
