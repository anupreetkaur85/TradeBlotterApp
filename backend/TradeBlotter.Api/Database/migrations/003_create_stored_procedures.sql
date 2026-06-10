-- Stored procedures for all trade data access. Using sprocs keeps the query plans
-- cached and the access path explicit, supporting the low-latency, minimal-LINQ goal.

CREATE OR ALTER PROCEDURE dbo.usp_Trade_Insert
    @Symbol          VARCHAR(16),
    @Side            CHAR(1),
    @Quantity        BIGINT,
    @Price           DECIMAL(19, 4),
    @ExecutedAtUtc   DATETIMEOFFSET(7),
    @ClientRequestId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Trades (Symbol, Side, Quantity, Price, ExecutedAtUtc, IsSeedData, SeedKey, ClientRequestId)
    OUTPUT INSERTED.TradeId, INSERTED.Symbol, INSERTED.Side, INSERTED.Quantity, INSERTED.Price, INSERTED.ExecutedAtUtc
    VALUES (@Symbol, @Side, @Quantity, @Price, @ExecutedAtUtc, 0, NULL, @ClientRequestId);
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Trade_GetByClientRequestId
    @ClientRequestId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TradeId, Symbol, Side, Quantity, Price, ExecutedAtUtc
    FROM dbo.Trades
    WHERE ClientRequestId = @ClientRequestId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Trade_GetNewestFirst
    @BeforeId BIGINT = NULL,
    @Limit    INT = 200
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Limit) TradeId, Symbol, Side, Quantity, Price, ExecutedAtUtc
    FROM dbo.Trades
    WHERE (@BeforeId IS NULL OR TradeId < @BeforeId)
    ORDER BY ExecutedAtUtc DESC, TradeId DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Trade_GetForPositions
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Symbol, Side, Quantity, Price
    FROM dbo.Trades
    ORDER BY Symbol ASC, ExecutedAtUtc ASC, TradeId ASC;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Trade_GetMaxId
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ISNULL(MAX(TradeId), 0) FROM dbo.Trades;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Seed_Count
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(1) FROM dbo.Trades WHERE IsSeedData = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Seed_Insert
    @Symbol        VARCHAR(16),
    @Side          CHAR(1),
    @Quantity      BIGINT,
    @Price         DECIMAL(19, 4),
    @ExecutedAtUtc DATETIMEOFFSET(7),
    @SeedKey       VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Trades (Symbol, Side, Quantity, Price, ExecutedAtUtc, IsSeedData, SeedKey)
    SELECT @Symbol, @Side, @Quantity, @Price, @ExecutedAtUtc, 1, @SeedKey
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Trades WHERE SeedKey = @SeedKey);
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Seed_Delete
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Trades WHERE IsSeedData = 1;
END
GO
