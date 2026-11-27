--⌄ enclosing_range_start scip-sql . postgres . shop/customer_totals#
  CREATE VIEW shop.customer_totals AS
--            ^^^^ reference scip-sql . postgres . shop/
--                 ^^^^^^^^^^^^^^^ definition scip-sql . postgres . shop/customer_totals#
--                                 kind Class
--                                 display_name customer_totals
--                                 documentation
--                                 > ```sql
--                                 > VIEW shop.customer_totals
--                                 > ```
  SELECT c.id AS customer_id, c.email, sum(o.total) AS lifetime_total
--       ^ reference local 0
--         ^^ reference scip-sql . postgres . shop/customers#id.
--               ^^^^^^^^^^^ definition scip-sql . postgres . shop/customer_totals#customer_id.
--                           kind Field
--                           display_name customer_id
--                           documentation
--                           > ```sql
--                           > customer_id (column of shop.customer_totals)
--                           > ```
--                           relationship scip-sql . postgres . shop/customers#id. reference
--                            ^ reference local 0
--                              ^^^^^ reference scip-sql . postgres . shop/customers#email.
--                              ^^^^^ definition scip-sql . postgres . shop/customer_totals#email.
--                                    kind Field
--                                    display_name email
--                                    documentation
--                                    > ```sql
--                                    > email (column of shop.customer_totals)
--                                    > ```
--                                    relationship scip-sql . postgres . shop/customers#email. reference
--                                         ^ reference local 1
--                                           ^^^^^ reference scip-sql . postgres . shop/orders#total.
--                                                     ^^^^^^^^^^^^^^ definition scip-sql . postgres . shop/customer_totals#lifetime_total.
--                                                                    kind Field
--                                                                    display_name lifetime_total
--                                                                    documentation
--                                                                    > ```sql
--                                                                    > lifetime_total (column of shop.customer_totals)
--                                                                    > ```
  FROM shop.customers c
--     ^^^^ reference scip-sql . postgres . shop/
--          ^^^^^^^^^ reference scip-sql . postgres . shop/customers#
--                    ^ definition local 0
--                      kind Variable
--                      display_name c
--                      documentation
--                      > ```sql
--                      > c: shop.customers
--                      > ```
  JOIN shop.orders o ON o.customer_id = c.id
--     ^^^^ reference scip-sql . postgres . shop/
--          ^^^^^^ reference scip-sql . postgres . shop/orders#
--                 ^ definition local 1
--                   kind Variable
--                   display_name o
--                   documentation
--                   > ```sql
--                   > o: shop.orders
--                   > ```
--                      ^ reference local 1
--                        ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
--                                      ^ reference local 0
--                                        ^^ reference scip-sql . postgres . shop/customers#id.
  WHERE o.status <> 'pending'
--      ^ reference local 1
--        ^^^^^^ reference scip-sql . postgres . shop/orders#status.
  GROUP BY c.id, c.email;
--         ^ reference local 0
--           ^^ reference scip-sql . postgres . shop/customers#id.
--               ^ reference local 0
--                 ^^^^^ reference scip-sql . postgres . shop/customers#email.
--                     ⌃ enclosing_range_end scip-sql . postgres . shop/customer_totals#
  
  WITH recent AS (
--     ^^^^^^ definition local 2
--            kind Class
--            display_name recent
--            documentation
--            > ```sql
--            > WITH recent AS (...)
--            > ```
      SELECT customer_id, max(id) AS last_order
--           ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
--                            ^^ reference scip-sql . postgres . shop/orders#id.
--                                   ^^^^^^^^^^ definition local 3
--                                              kind Variable
--                                              display_name last_order
--                                              documentation
--                                              > ```sql
--                                              > column alias last_order
--                                              > ```
      FROM orders
--         ^^^^^^ reference scip-sql . postgres . shop/orders#
      GROUP BY customer_id
--             ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
  )
  SELECT r.customer_id, r.last_order, ct.lifetime_total
--       ^ reference local 4
--         ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
--                      ^ reference local 4
--                        ^^^^^^^^^^ reference local 3
--                                    ^^ reference local 5
--                                       ^^^^^^^^^^^^^^ reference scip-sql . postgres . shop/customer_totals#lifetime_total.
  FROM recent r
--     ^^^^^^ reference local 2
--            ^ definition local 4
--              kind Variable
--              display_name r
--              documentation
--              > ```sql
--              > r: WITH recent
--              > ```
  LEFT JOIN shop.customer_totals ct USING (customer_id)
--          ^^^^ reference scip-sql . postgres . shop/
--               ^^^^^^^^^^^^^^^ reference scip-sql . postgres . shop/customer_totals#
--                               ^^ definition local 5
--                                  kind Variable
--                                  display_name ct
--                                  documentation
--                                  > ```sql
--                                  > ct: shop.customer_totals
--                                  > ```
--                                         ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
  WHERE EXISTS (SELECT 1 FROM orders o2 WHERE o2.customer_id = r.customer_id AND o2.total > 100)
--                            ^^^^^^ reference scip-sql . postgres . shop/orders#
--                                   ^^ definition local 6
--                                      kind Variable
--                                      display_name o2
--                                      documentation
--                                      > ```sql
--                                      > o2: orders
--                                      > ```
--                                            ^^ reference local 6
--                                               ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
--                                                             ^ reference local 4
--                                                               ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
--                                                                               ^^ reference local 6
--                                                                                  ^^^^^ reference scip-sql . postgres . shop/orders#total.
  ORDER BY last_order DESC;
--         ^^^^^^^^^^ reference local 3
  
