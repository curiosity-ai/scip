USE Shop
GO
CREATE SCHEMA Sales AUTHORIZATION dbo
GO
-- Product catalogue
CREATE TABLE [Sales].[Products] (
    ProductID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY CLUSTERED,
    [Name] NVARCHAR(100) NOT NULL,
    ListPrice MONEY NOT NULL DEFAULT 0,
    Discounted AS (ListPrice * 0.9)
)
GO
CREATE TABLE Sales.OrderLines (
    OrderLineID INT IDENTITY PRIMARY KEY,
    ProductID INT NOT NULL,
    Quantity INT NOT NULL,
    CONSTRAINT FK_OrderLines_Products FOREIGN KEY (ProductID) REFERENCES Sales.Products (ProductID)
)
GO
CREATE NONCLUSTERED INDEX IX_OrderLines_Product ON Sales.OrderLines (ProductID) INCLUDE (Quantity)
GO
CREATE OR ALTER PROCEDURE Sales.usp_TopProducts
    @MinQuantity INT = 10,
    @Top INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Count INT = 0, @Totals TABLE (ProductID INT, Total INT);
    INSERT INTO @Totals (ProductID, Total)
    SELECT ol.ProductID, SUM(ol.Quantity)
    FROM Sales.OrderLines AS ol
    GROUP BY ol.ProductID
    HAVING SUM(ol.Quantity) >= @MinQuantity;
    SELECT TOP (@Top) p.[Name], t.Total
    FROM @Totals t
    INNER JOIN Sales.Products p ON p.ProductID = t.ProductID
    ORDER BY t.Total DESC;
    SET @Count = @@ROWCOUNT;
    RETURN @Count;
END
GO
CREATE TRIGGER Sales.trg_OrderLines_Audit ON Sales.OrderLines AFTER INSERT, UPDATE
AS
    UPDATE p SET ListPrice = p.ListPrice
    FROM Sales.Products p JOIN inserted i ON i.ProductID = p.ProductID
GO
EXEC Sales.usp_TopProducts @MinQuantity = 5, @Top = 3
GO
MERGE INTO Sales.Products AS tgt
USING (SELECT ProductID, SUM(Quantity) AS Qty FROM Sales.OrderLines GROUP BY ProductID) AS src
ON tgt.ProductID = src.ProductID
WHEN MATCHED THEN UPDATE SET ListPrice = tgt.ListPrice + src.Qty
WHEN NOT MATCHED THEN INSERT (Name, ListPrice) VALUES ('new', 0);
GO
