-- The assessment contract requires GET /trades to return all trades newest
-- first. Pagination can be introduced later as a separate, explicitly
-- versioned contract.
CREATE OR ALTER PROCEDURE dbo.usp_Trade_GetNewestFirst
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TradeId, Symbol, Side, Quantity, Price, ExecutedAtUtc
    FROM dbo.Trades
    ORDER BY ExecutedAtUtc DESC, TradeId DESC;
END
GO

DROP PROCEDURE IF EXISTS dbo.usp_Trade_GetMaxId;
GO
