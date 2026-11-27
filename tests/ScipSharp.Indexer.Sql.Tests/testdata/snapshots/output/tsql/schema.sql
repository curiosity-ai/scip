  USE Shop
--    ^^^^ reference scip-sql . tsql . shop/
  GO
--⌄ enclosing_range_start scip-sql . tsql . shop/sales/
  CREATE SCHEMA Sales AUTHORIZATION dbo
--              ^^^^^ definition scip-sql . tsql . shop/sales/
--                    kind Namespace
--                    display_name Sales
--                    documentation
--                    > ```sql
--                    > SCHEMA Sales
--                    > ```
--                                    ⌃ enclosing_range_end scip-sql . tsql . shop/sales/
  GO
  -- Product catalogue
--⌄ enclosing_range_start scip-sql . tsql . shop/sales/products#
  CREATE TABLE [Sales].[Products] (
--             ^^^^^^^ reference scip-sql . tsql . shop/sales/
--                     ^^^^^^^^^^ definition scip-sql . tsql . shop/sales/products#
--                                kind Class
--                                display_name Products
--                                documentation
--                                > ```sql
--                                > TABLE shop.sales.Products
--                                > ```
--                                documentation
--                                > Product catalogue
--    ⌄ enclosing_range_start scip-sql . tsql . shop/sales/products#productid.
--                                         ⌄ enclosing_range_start scip-sql . tsql . shop/sales/products#pk_products:
      ProductID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY CLUSTERED,
--    ^^^^^^^^^ definition scip-sql . tsql . shop/sales/products#productid.
--              kind Field
--              display_name ProductID
--              documentation
--              > ```sql
--              > ProductID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY CLUSTERED
--              > ```
--                                                    ^^^^^^^^^^^ definition scip-sql . tsql . shop/sales/products#pk_products:
--                                                                kind Key
--                                                                display_name PK_Products
--                                                                documentation
--                                                                > ```sql
--                                                                > CONSTRAINT PK_Products PRIMARY KEY
--                                                                > ```
--                                                                          ⌃ enclosing_range_end scip-sql . tsql . shop/sales/products#pk_products:
--                                                                                    ⌃ enclosing_range_end scip-sql . tsql . shop/sales/products#productid.
--    ⌄ enclosing_range_start scip-sql . tsql . shop/sales/products#name.
      [Name] NVARCHAR(100) NOT NULL,
--    ^^^^^^ definition scip-sql . tsql . shop/sales/products#name.
--           kind Field
--           display_name Name
--           documentation
--           > ```sql
--           > [Name] NVARCHAR(100) NOT NULL
--           > ```
--                                ⌃ enclosing_range_end scip-sql . tsql . shop/sales/products#name.
--    ⌄ enclosing_range_start scip-sql . tsql . shop/sales/products#listprice.
      ListPrice MONEY NOT NULL DEFAULT 0,
--    ^^^^^^^^^ definition scip-sql . tsql . shop/sales/products#listprice.
--              kind Field
--              display_name ListPrice
--              documentation
--              > ```sql
--              > ListPrice MONEY NOT NULL DEFAULT 0
--              > ```
--                                     ⌃ enclosing_range_end scip-sql . tsql . shop/sales/products#listprice.
--    ⌄ enclosing_range_start scip-sql . tsql . shop/sales/products#discounted.
      Discounted AS (ListPrice * 0.9)
--    ^^^^^^^^^^ definition scip-sql . tsql . shop/sales/products#discounted.
--               kind Field
--               display_name Discounted
--               documentation
--               > ```sql
--               > Discounted AS (ListPrice * 0.9)
--               > ```
--                   ^^^^^^^^^ reference scip-sql . tsql . shop/sales/products#listprice.
--                                  ⌃ enclosing_range_end scip-sql . tsql . shop/sales/products#discounted.
  )
--⌃ enclosing_range_end scip-sql . tsql . shop/sales/products#
  GO
--⌄ enclosing_range_start scip-sql . tsql . shop/sales/orderlines#
  CREATE TABLE Sales.OrderLines (
--             ^^^^^ reference scip-sql . tsql . shop/sales/
--                   ^^^^^^^^^^ definition scip-sql . tsql . shop/sales/orderlines#
--                              kind Class
--                              display_name OrderLines
--                              documentation
--                              > ```sql
--                              > TABLE shop.sales.OrderLines
--                              > ```
--    ⌄ enclosing_range_start scip-sql . tsql . shop/sales/orderlines#orderlineid.
      OrderLineID INT IDENTITY PRIMARY KEY,
--    ^^^^^^^^^^^ definition scip-sql . tsql . shop/sales/orderlines#orderlineid.
--                kind Field
--                display_name OrderLineID
--                documentation
--                > ```sql
--                > OrderLineID INT IDENTITY PRIMARY KEY
--                > ```
--                                       ⌃ enclosing_range_end scip-sql . tsql . shop/sales/orderlines#orderlineid.
--    ⌄ enclosing_range_start scip-sql . tsql . shop/sales/orderlines#productid.
      ProductID INT NOT NULL,
--    ^^^^^^^^^ definition scip-sql . tsql . shop/sales/orderlines#productid.
--              kind Field
--              display_name ProductID
--              documentation
--              > ```sql
--              > ProductID INT NOT NULL
--              > ```
--              relationship scip-sql . tsql . shop/sales/products#productid. reference
--                         ⌃ enclosing_range_end scip-sql . tsql . shop/sales/orderlines#productid.
--    ⌄ enclosing_range_start scip-sql . tsql . shop/sales/orderlines#quantity.
      Quantity INT NOT NULL,
--    ^^^^^^^^ definition scip-sql . tsql . shop/sales/orderlines#quantity.
--             kind Field
--             display_name Quantity
--             documentation
--             > ```sql
--             > Quantity INT NOT NULL
--             > ```
--                        ⌃ enclosing_range_end scip-sql . tsql . shop/sales/orderlines#quantity.
--    ⌄ enclosing_range_start scip-sql . tsql . shop/sales/orderlines#fk_orderlines_products:
      CONSTRAINT FK_OrderLines_Products FOREIGN KEY (ProductID) REFERENCES Sales.Products (ProductID)
--               ^^^^^^^^^^^^^^^^^^^^^^ definition scip-sql . tsql . shop/sales/orderlines#fk_orderlines_products:
--                                      kind Key
--                                      display_name FK_OrderLines_Products
--                                      documentation
--                                      > ```sql
--                                      > CONSTRAINT FK_OrderLines_Products FOREIGN KEY (ProductID) REFERENCES Sales.Products (ProductID)
--                                      > ```
--                                                   ^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#productid.
--                                                                         ^^^^^ reference scip-sql . tsql . shop/sales/
--                                                                               ^^^^^^^^ reference scip-sql . tsql . shop/sales/products#
--                                                                                         ^^^^^^^^^ reference scip-sql . tsql . shop/sales/products#productid.
--                                                                                                  ⌃ enclosing_range_end scip-sql . tsql . shop/sales/orderlines#fk_orderlines_products:
  )
--⌃ enclosing_range_end scip-sql . tsql . shop/sales/orderlines#
  GO
--⌄ enclosing_range_start scip-sql . tsql . shop/sales/orderlines#ix_orderlines_product:
  CREATE NONCLUSTERED INDEX IX_OrderLines_Product ON Sales.OrderLines (ProductID) INCLUDE (Quantity)
--                          ^^^^^^^^^^^^^^^^^^^^^ definition scip-sql . tsql . shop/sales/orderlines#ix_orderlines_product:
--                                                kind Key
--                                                display_name IX_OrderLines_Product
--                                                documentation
--                                                > ```sql
--                                                > INDEX IX_OrderLines_Product ON shop.sales.OrderLines
--                                                > ```
--                                                   ^^^^^ reference scip-sql . tsql . shop/sales/
--                                                         ^^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#
--                                                                     ^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#productid.
--                                                                                         ^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#quantity.
--                                                                                                 ⌃ enclosing_range_end scip-sql . tsql . shop/sales/orderlines#ix_orderlines_product:
  GO
--⌄ enclosing_range_start scip-sql . tsql . shop/sales/usp_topproducts().
  CREATE OR ALTER PROCEDURE Sales.usp_TopProducts
--                          ^^^^^ reference scip-sql . tsql . shop/sales/
--                                ^^^^^^^^^^^^^^^ definition scip-sql . tsql . shop/sales/usp_topproducts().
--                                                kind Method
--                                                display_name usp_TopProducts
--                                                documentation
--                                                > ```sql
--                                                > PROCEDURE shop.sales.usp_TopProducts(@MinQuantity INT, OUT @Top INT)
--                                                > ```
      @MinQuantity INT = 10,
--    ^^^^^^^^^^^^ definition scip-sql . tsql . shop/sales/usp_topproducts().(minquantity)
--                 kind Parameter
--                 display_name @MinQuantity
--                 documentation
--                 > ```sql
--                 > @MinQuantity INT
--                 > ```
      @Top INT OUTPUT
--    ^^^^ definition scip-sql . tsql . shop/sales/usp_topproducts().(top)
--         kind Parameter
--         display_name @Top
--         documentation
--         > ```sql
--         > OUT @Top INT
--         > ```
  AS
  BEGIN
      SET NOCOUNT ON;
--            ⌄ enclosing_range_start local 0
--                            ⌄ enclosing_range_start local 1
      DECLARE @Count INT = 0, @Totals TABLE (ProductID INT, Total INT);
--            ^^^^^^ definition local 0
--                   kind Variable
--                   display_name @Count
--                   documentation
--                   > ```sql
--                   > DECLARE @Count INT
--                   > ```
--                            ^^^^^^^ definition local 1
--                                    kind Variable
--                                    display_name @Totals
--                                    documentation
--                                    > ```sql
--                                    > DECLARE @Totals TABLE
--                                    > ```
--                                           ^^^^^^^^^ definition local 2
--                                                     kind Field
--                                                     display_name ProductID
--                                                     documentation
--                                                     > ```sql
--                                                     > ProductID INT
--                                                     > ```
--                                                          ^^^^^ definition local 3
--                                                                kind Field
--                                                                display_name Total
--                                                                documentation
--                                                                > ```sql
--                                                                > Total INT
--                                                                > ```
--                         ⌃ enclosing_range_end local 0
--                                                                   ⌃ enclosing_range_end local 1
      INSERT INTO @Totals (ProductID, Total)
--                ^^^^^^^ reference local 1
--                         ^^^^^^^^^ reference local 2
--                                    ^^^^^ reference local 3
      SELECT ol.ProductID, SUM(ol.Quantity)
--           ^^ reference local 4
--              ^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#productid.
--                             ^^ reference local 4
--                                ^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#quantity.
      FROM Sales.OrderLines AS ol
--         ^^^^^ reference scip-sql . tsql . shop/sales/
--               ^^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#
--                             ^^ definition local 4
--                                kind Variable
--                                display_name ol
--                                documentation
--                                > ```sql
--                                > ol: Sales.OrderLines
--                                > ```
      GROUP BY ol.ProductID
--             ^^ reference local 4
--                ^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#productid.
      HAVING SUM(ol.Quantity) >= @MinQuantity;
--               ^^ reference local 4
--                  ^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#quantity.
--                               ^^^^^^^^^^^^ reference scip-sql . tsql . shop/sales/usp_topproducts().(minquantity)
      SELECT TOP (@Top) p.[Name], t.Total
--                ^^^^ reference scip-sql . tsql . shop/sales/usp_topproducts().(top)
--                      ^ reference local 6
--                        ^^^^^^ reference scip-sql . tsql . shop/sales/products#name.
--                                ^ reference local 5
--                                  ^^^^^ reference local 3
      FROM @Totals t
--         ^^^^^^^ reference local 1
--                 ^ definition local 5
--                   kind Variable
--                   display_name t
--                   documentation
--                   > ```sql
--                   > t: @Totals
--                   > ```
      INNER JOIN Sales.Products p ON p.ProductID = t.ProductID
--               ^^^^^ reference scip-sql . tsql . shop/sales/
--                     ^^^^^^^^ reference scip-sql . tsql . shop/sales/products#
--                              ^ definition local 6
--                                kind Variable
--                                display_name p
--                                documentation
--                                > ```sql
--                                > p: Sales.Products
--                                > ```
--                                   ^ reference local 6
--                                     ^^^^^^^^^ reference scip-sql . tsql . shop/sales/products#productid.
--                                                 ^ reference local 5
--                                                   ^^^^^^^^^ reference local 2
      ORDER BY t.Total DESC;
--             ^ reference local 5
--               ^^^^^ reference local 3
      SET @Count = @@ROWCOUNT;
--        ^^^^^^ reference local 0
      RETURN @Count;
--           ^^^^^^ reference local 0
  END
--  ⌃ enclosing_range_end scip-sql . tsql . shop/sales/usp_topproducts().
  GO
--⌄ enclosing_range_start scip-sql . tsql . shop/sales/orderlines#trg_orderlines_audit:
  CREATE TRIGGER Sales.trg_OrderLines_Audit ON Sales.OrderLines AFTER INSERT, UPDATE
--                     ^^^^^^^^^^^^^^^^^^^^ definition scip-sql . tsql . shop/sales/orderlines#trg_orderlines_audit:
--                                          kind Event
--                                          display_name trg_OrderLines_Audit
--                                          documentation
--                                          > ```sql
--                                          > TRIGGER trg_OrderLines_Audit ON shop.sales.OrderLines
--                                          > ```
--                                             ^^^^^ reference scip-sql . tsql . shop/sales/
--                                                   ^^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#
  AS
      UPDATE p SET ListPrice = p.ListPrice
--           ^ reference local 7
--                 ^^^^^^^^^ reference scip-sql . tsql . shop/sales/products#listprice.
--                             ^ reference local 7
--                               ^^^^^^^^^ reference scip-sql . tsql . shop/sales/products#listprice.
      FROM Sales.Products p JOIN inserted i ON i.ProductID = p.ProductID
--         ^^^^^ reference scip-sql . tsql . shop/sales/
--               ^^^^^^^^ reference scip-sql . tsql . shop/sales/products#
--                        ^ definition local 7
--                          kind Variable
--                          display_name p
--                          documentation
--                          > ```sql
--                          > p: Sales.Products
--                          > ```
--                                        ^ definition local 8
--                                          kind Variable
--                                          display_name i
--                                          documentation
--                                          > ```sql
--                                          > i: inserted
--                                          > ```
--                                             ^ reference local 8
--                                               ^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#productid.
--                                                           ^ reference local 7
--                                                             ^^^^^^^^^ reference scip-sql . tsql . shop/sales/products#productid.
--                                                                     ⌃ enclosing_range_end scip-sql . tsql . shop/sales/orderlines#trg_orderlines_audit:
  GO
  EXEC Sales.usp_TopProducts @MinQuantity = 5, @Top = 3
--     ^^^^^ reference scip-sql . tsql . shop/sales/
--           ^^^^^^^^^^^^^^^ reference scip-sql . tsql . shop/sales/usp_topproducts().
--                           ^^^^^^^^^^^^ reference scip-sql . tsql . shop/sales/usp_topproducts().(minquantity)
--                                             ^^^^ reference scip-sql . tsql . shop/sales/usp_topproducts().(top)
  GO
  MERGE INTO Sales.Products AS tgt
--           ^^^^^ reference scip-sql . tsql . shop/sales/
--                 ^^^^^^^^ reference scip-sql . tsql . shop/sales/products#
--                             ^^^ definition local 9
--                                 kind Variable
--                                 display_name tgt
--                                 documentation
--                                 > ```sql
--                                 > tgt: Sales.Products
--                                 > ```
  USING (SELECT ProductID, SUM(Quantity) AS Qty FROM Sales.OrderLines GROUP BY ProductID) AS src
--              ^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#productid.
--                             ^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#quantity.
--                                          ^^^ definition local 10
--                                              kind Variable
--                                              display_name Qty
--                                              documentation
--                                              > ```sql
--                                              > column alias Qty
--                                              > ```
--                                                   ^^^^^ reference scip-sql . tsql . shop/sales/
--                                                         ^^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#
--                                                                             ^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#productid.
--                                                                                           ^^^ definition local 11
--                                                                                               kind Variable
--                                                                                               display_name src
--                                                                                               documentation
--                                                                                               > ```sql
--                                                                                               > src: derived table
--                                                                                               > ```
  ON tgt.ProductID = src.ProductID
--   ^^^ reference local 9
--       ^^^^^^^^^ reference scip-sql . tsql . shop/sales/products#productid.
--                   ^^^ reference local 11
--                       ^^^^^^^^^ reference scip-sql . tsql . shop/sales/orderlines#productid.
  WHEN MATCHED THEN UPDATE SET ListPrice = tgt.ListPrice + src.Qty
--                             ^^^^^^^^^ reference scip-sql . tsql . shop/sales/products#listprice.
--                                         ^^^ reference local 9
--                                             ^^^^^^^^^ reference scip-sql . tsql . shop/sales/products#listprice.
--                                                         ^^^ reference local 11
--                                                             ^^^ reference local 10
  WHEN NOT MATCHED THEN INSERT (Name, ListPrice) VALUES ('new', 0);
--                              ^^^^ reference scip-sql . tsql . shop/sales/products#name.
--                                    ^^^^^^^^^ reference scip-sql . tsql . shop/sales/products#listprice.
  GO
  
