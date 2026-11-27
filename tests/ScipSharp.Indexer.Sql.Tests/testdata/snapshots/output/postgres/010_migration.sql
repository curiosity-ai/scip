--                              ⌄ enclosing_range_start scip-sql . postgres . shop/orders#shipped_at.
  ALTER TABLE orders ADD COLUMN shipped_at timestamptz;
--            ^^^^^^ reference scip-sql . postgres . shop/orders#
--                              ^^^^^^^^^^ definition scip-sql . postgres . shop/orders#shipped_at.
--                                         kind Field
--                                         display_name shipped_at
--                                         documentation
--                                         > ```sql
--                                         > shipped_at timestamptz
--                                         > ```
--                                                   ⌃ enclosing_range_end scip-sql . postgres . shop/orders#shipped_at.
--                      ⌄ enclosing_range_start scip-sql . postgres . shop/customers#display_name.
  ALTER TABLE customers RENAME COLUMN full_name TO display_name;
--            ^^^^^^^^^ reference scip-sql . postgres . shop/customers#
--                                    ^^^^^^^^^ reference scip-sql . postgres . shop/customers#full_name.
--                                                 ^^^^^^^^^^^^ definition scip-sql . postgres . shop/customers#display_name.
--                                                              kind Field
--                                                              display_name display_name
--                                                              documentation
--                                                              > ```sql
--                                                              > RENAME COLUMN full_name TO display_name
--                                                              > ```
--                                                              documentation
--                                                              > Renamed from full_name.
--                                                              relationship scip-sql . postgres . shop/customers#full_name. reference
--                                                            ⌃ enclosing_range_end scip-sql . postgres . shop/customers#display_name.
  UPDATE customers SET display_name = upper(display_name) WHERE id IN (SELECT customer_id FROM orders);
--       ^^^^^^^^^ reference scip-sql . postgres . shop/customers#
--                     ^^^^^^^^^^^^ reference scip-sql . postgres . shop/customers#display_name.
--                                          ^^^^^^^^^^^^ reference scip-sql . postgres . shop/customers#display_name.
--                                                              ^^ reference scip-sql . postgres . shop/customers#id.
--                                                                            ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
--                                                                                             ^^^^^^ reference scip-sql . postgres . shop/orders#
  INSERT INTO orders (customer_id, status, total) VALUES (1, 'paid', 10.5)
--            ^^^^^^ reference scip-sql . postgres . shop/orders#
--                    ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
--                                 ^^^^^^ reference scip-sql . postgres . shop/orders#status.
--                                         ^^^^^ reference scip-sql . postgres . shop/orders#total.
  ON CONFLICT (id) DO UPDATE SET total = EXCLUDED.total
--             ^^ reference scip-sql . postgres . shop/orders#id.
--                               ^^^^^ reference scip-sql . postgres . shop/orders#total.
--                                                ^^^^^ reference scip-sql . postgres . shop/orders#total.
  RETURNING id, shipped_at;
--          ^^ reference scip-sql . postgres . shop/orders#id.
--              ^^^^^^^^^^ reference scip-sql . postgres . shop/orders#shipped_at.
  DELETE FROM orders o USING customers c WHERE o.customer_id = c.id AND c.email LIKE '%@example.com';
--            ^^^^^^ reference scip-sql . postgres . shop/orders#
--                   ^ definition local 0
--                     kind Variable
--                     display_name o
--                     documentation
--                     > ```sql
--                     > o: orders
--                     > ```
--                           ^^^^^^^^^ reference scip-sql . postgres . shop/customers#
--                                     ^ definition local 1
--                                       kind Variable
--                                       display_name c
--                                       documentation
--                                       > ```sql
--                                       > c: customers
--                                       > ```
--                                             ^ reference local 0
--                                               ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
--                                                             ^ reference local 1
--                                                               ^^ reference scip-sql . postgres . shop/customers#id.
--                                                                      ^ reference local 1
--                                                                        ^^^^^ reference scip-sql . postgres . shop/customers#email.
  SELECT * FROM unknown_table u WHERE u.flag;
--              ^^^^^^^^^^^^^ reference scip-sql . postgres . public/unknown_table#
--                            ^ definition local 2
--                              kind Variable
--                              display_name u
--                              documentation
--                              > ```sql
--                              > u: unknown_table
--                              > ```
--                                    ^ reference local 2
--                                      ^^^^ reference scip-sql . postgres . public/unknown_table#flag.
  
