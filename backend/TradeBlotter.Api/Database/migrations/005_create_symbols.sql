-- Reference symbol universe for the trade-entry autocomplete. This is read-only
-- reference data (not the toggle-able trade seed), so it is seeded directly here.

IF OBJECT_ID(N'dbo.Symbols', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Symbols
    (
        Symbol VARCHAR(16)   NOT NULL CONSTRAINT PK_Symbols PRIMARY KEY,
        Name   NVARCHAR(128) NOT NULL
    );
END
GO

-- Idempotent reference seed: insert only the symbols not already present.
INSERT INTO dbo.Symbols (Symbol, Name)
SELECT v.Symbol, v.Name
FROM (VALUES
    ('AAPL',  N'Apple Inc.'),
    ('MSFT',  N'Microsoft Corporation'),
    ('GOOGL', N'Alphabet Inc. Class A'),
    ('GOOG',  N'Alphabet Inc. Class C'),
    ('AMZN',  N'Amazon.com, Inc.'),
    ('NVDA',  N'NVIDIA Corporation'),
    ('META',  N'Meta Platforms, Inc.'),
    ('TSLA',  N'Tesla, Inc.'),
    ('BRK.B', N'Berkshire Hathaway Inc. Class B'),
    ('JPM',   N'JPMorgan Chase & Co.'),
    ('V',     N'Visa Inc.'),
    ('MA',    N'Mastercard Incorporated'),
    ('UNH',   N'UnitedHealth Group Incorporated'),
    ('HD',    N'The Home Depot, Inc.'),
    ('PG',    N'The Procter & Gamble Company'),
    ('JNJ',   N'Johnson & Johnson'),
    ('XOM',   N'Exxon Mobil Corporation'),
    ('CVX',   N'Chevron Corporation'),
    ('KO',    N'The Coca-Cola Company'),
    ('PEP',   N'PepsiCo, Inc.'),
    ('COST',  N'Costco Wholesale Corporation'),
    ('WMT',   N'Walmart Inc.'),
    ('DIS',   N'The Walt Disney Company'),
    ('NFLX',  N'Netflix, Inc.'),
    ('ADBE',  N'Adobe Inc.'),
    ('CRM',   N'Salesforce, Inc.'),
    ('INTC',  N'Intel Corporation'),
    ('AMD',   N'Advanced Micro Devices, Inc.'),
    ('ORCL',  N'Oracle Corporation'),
    ('CSCO',  N'Cisco Systems, Inc.'),
    ('BAC',   N'Bank of America Corporation'),
    ('WFC',   N'Wells Fargo & Company'),
    ('GS',    N'The Goldman Sachs Group, Inc.'),
    ('MS',    N'Morgan Stanley'),
    ('BA',    N'The Boeing Company'),
    ('PFE',   N'Pfizer Inc.'),
    ('T',     N'AT&T Inc.'),
    ('VZ',    N'Verizon Communications Inc.'),
    ('NKE',   N'NIKE, Inc.'),
    ('PYPL',  N'PayPal Holdings, Inc.')
) AS v(Symbol, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Symbols s WHERE s.Symbol = v.Symbol);
GO

CREATE OR ALTER PROCEDURE dbo.usp_Symbol_Search
    @Query NVARCHAR(64),
    @Limit INT = 8
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @q NVARCHAR(64) = UPPER(LTRIM(RTRIM(@Query)));

    IF @q = N''
    BEGIN
        SELECT TOP (@Limit) Symbol, Name
        FROM dbo.Symbols
        ORDER BY Symbol;
        RETURN;
    END

    -- Rank: exact symbol, then symbol prefix, then symbol contains, then name contains.
    SELECT TOP (@Limit) Symbol, Name
    FROM dbo.Symbols
    WHERE Symbol LIKE '%' + @q + '%' OR UPPER(Name) LIKE '%' + @q + '%'
    ORDER BY
        CASE
            WHEN Symbol = @q THEN 0
            WHEN Symbol LIKE @q + '%' THEN 1
            WHEN Symbol LIKE '%' + @q + '%' THEN 2
            ELSE 3
        END,
        LEN(Symbol),
        Symbol;
END
GO
