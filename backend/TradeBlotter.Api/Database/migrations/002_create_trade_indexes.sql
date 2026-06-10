-- Blotter read path: newest first.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Trades_Blotter' AND object_id = OBJECT_ID(N'dbo.Trades'))
    CREATE INDEX IX_Trades_Blotter
        ON dbo.Trades (ExecutedAtUtc DESC, TradeId DESC);

-- Positions read path: chronological scan per symbol, covered.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Trades_Symbol_Chrono' AND object_id = OBJECT_ID(N'dbo.Trades'))
    CREATE INDEX IX_Trades_Symbol_Chrono
        ON dbo.Trades (Symbol, ExecutedAtUtc, TradeId)
        INCLUDE (Side, Quantity, Price);

-- Idempotent create: one row per client request id.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Trades_ClientRequestId' AND object_id = OBJECT_ID(N'dbo.Trades'))
    CREATE UNIQUE INDEX UX_Trades_ClientRequestId
        ON dbo.Trades (ClientRequestId)
        WHERE ClientRequestId IS NOT NULL;

-- Supports fast seed-row counting and deletion.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Trades_IsSeedData' AND object_id = OBJECT_ID(N'dbo.Trades'))
    CREATE INDEX IX_Trades_IsSeedData
        ON dbo.Trades (IsSeedData)
        WHERE IsSeedData = 1;

-- Constraint: each seed row's stable key appears at most once (idempotent seeding).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Trades_SeedKey' AND object_id = OBJECT_ID(N'dbo.Trades'))
    CREATE UNIQUE INDEX UX_Trades_SeedKey
        ON dbo.Trades (SeedKey)
        WHERE SeedKey IS NOT NULL;
