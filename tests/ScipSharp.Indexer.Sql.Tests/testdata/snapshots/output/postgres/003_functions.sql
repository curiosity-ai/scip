--⌄ enclosing_range_start scip-sql . postgres . shop/order_count(integer).
  CREATE OR REPLACE FUNCTION shop.order_count(p_customer integer)
--                           ^^^^ reference scip-sql . postgres . shop/
--                                ^^^^^^^^^^^ definition scip-sql . postgres . shop/order_count(integer).
--                                            kind Function
--                                            display_name order_count
--                                            documentation
--                                            > ```sql
--                                            > FUNCTION shop.order_count(p_customer integer) RETURNS integer
--                                            > ```
--                                            ^^^^^^^^^^ definition scip-sql . postgres . shop/order_count(integer).(p_customer)
--                                                       kind Parameter
--                                                       display_name p_customer
--                                                       documentation
--                                                       > ```sql
--                                                       > p_customer integer
--                                                       > ```
  RETURNS integer
  LANGUAGE plpgsql AS $$
  DECLARE
--    ⌄ enclosing_range_start local 0
      v_count integer := 0;
--    ^^^^^^^ definition local 0
--            kind Variable
--            display_name v_count
--            documentation
--            > ```sql
--            > DECLARE v_count integer
--            > ```
--                       ⌃ enclosing_range_end local 0
  BEGIN
      SELECT count(*) INTO v_count FROM shop.orders WHERE customer_id = p_customer;
--                         ^^^^^^^ reference local 0
--                                      ^^^^ reference scip-sql . postgres . shop/
--                                           ^^^^^^ reference scip-sql . postgres . shop/orders#
--                                                        ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
--                                                                      ^^^^^^^^^^ reference scip-sql . postgres . shop/order_count(integer).(p_customer)
      RETURN v_count;
--           ^^^^^^^ reference local 0
  END;
  $$;
-- ⌃ enclosing_range_end scip-sql . postgres . shop/order_count(integer).
  
--⌄ enclosing_range_start scip-sql . postgres . shop/touch().
  CREATE FUNCTION shop.touch() RETURNS trigger AS $$
--                ^^^^ reference scip-sql . postgres . shop/
--                     ^^^^^ definition scip-sql . postgres . shop/touch().
--                           kind Function
--                           display_name touch
--                           documentation
--                           > ```sql
--                           > FUNCTION shop.touch() RETURNS trigger
--                           > ```
  BEGIN
      NEW.created_at := now();
--        ^^^^^^^^^^ reference scip-sql . postgres . shop/customers#created_at.
      RETURN NEW;
  END $$ LANGUAGE plpgsql;
--                      ⌃ enclosing_range_end scip-sql . postgres . shop/touch().
  
--⌄ enclosing_range_start scip-sql . postgres . shop/customers#customers_touch:
  CREATE TRIGGER customers_touch BEFORE INSERT ON shop.customers
--               ^^^^^^^^^^^^^^^ definition scip-sql . postgres . shop/customers#customers_touch:
--                               kind Event
--                               display_name customers_touch
--                               documentation
--                               > ```sql
--                               > TRIGGER customers_touch ON shop.customers
--                               > ```
--                                                ^^^^ reference scip-sql . postgres . shop/
--                                                     ^^^^^^^^^ reference scip-sql . postgres . shop/customers#
  FOR EACH ROW EXECUTE FUNCTION shop.touch();
--                              ^^^^ reference scip-sql . postgres . shop/
--                                   ^^^^^ reference scip-sql . postgres . shop/touch().
--                                         ⌃ enclosing_range_end scip-sql . postgres . shop/customers#customers_touch:
  
  SELECT shop.order_count(id) FROM customers;
--       ^^^^ reference scip-sql . postgres . shop/
--            ^^^^^^^^^^^ reference scip-sql . postgres . shop/order_count(integer).
--                        ^^ reference scip-sql . postgres . shop/customers#id.
--                                 ^^^^^^^^^ reference scip-sql . postgres . shop/customers#
  
