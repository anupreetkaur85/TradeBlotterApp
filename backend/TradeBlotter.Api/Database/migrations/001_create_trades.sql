IF OBJECT_ID(N'dbo.Trades', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Trades
    (
        TradeId         BIGINT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_Trades PRIMARY KEY,
        Symbol          VARCHAR(16)       NOT NULL,
        Side            CHAR(1)           NOT NULL,            -- 'B' = Buy, 'S' = Sell
        Quantity        BIGINT            NOT NULL,
        Price           DECIMAL(19, 4)    NOT NULL,
        ExecutedAtUtc   DATETIMEOFFSET(7) NOT NULL,
        IsSeedData      BIT               NOT NULL CONSTRAINT DF_Trades_IsSeedData DEFAULT (0),
        SeedKey         VARCHAR(50)       NULL,
        ClientRequestId UNIQUEIDENTIFIER  NULL,
        CONSTRAINT CK_Trades_Side     CHECK (Side IN ('B', 'S')),
        CONSTRAINT CK_Trades_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_Trades_Price    CHECK (Price > 0)
    );
END
