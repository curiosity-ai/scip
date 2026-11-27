  -- Shop schema (PostgreSQL)
--⌄ enclosing_range_start scip-sql . postgres . shop/
  CREATE SCHEMA shop;
--              ^^^^ definition scip-sql . postgres . shop/
--                   kind Namespace
--                   display_name shop
--                   documentation
--                   > ```sql
--                   > SCHEMA shop
--                   > ```
--                   documentation
--                   > Shop schema (PostgreSQL)
--                 ⌃ enclosing_range_end scip-sql . postgres . shop/
  SET search_path TO shop, public;
--                   ^^^^ reference scip-sql . postgres . shop/
--                         ^^^^^^ reference scip-sql . postgres . public/
  
--⌄ enclosing_range_start scip-sql . postgres . shop/order_status#
  CREATE TYPE order_status AS ENUM ('pending', 'paid', 'shipped');
--            ^^^^^^^^^^^^ definition scip-sql . postgres . shop/order_status#
--                         kind Type
--                         display_name order_status
--                         documentation
--                         > ```sql
--                         > TYPE (ENUM) shop.order_status
--                         > ```
--                                   ^^^^^^^ definition scip-sql . postgres . shop/order_status#pending.
--                                           kind EnumMember
--                                           display_name pending
--                                           documentation
--                                           > ```sql
--                                           > 'pending' (shop.order_status)
--                                           > ```
--                                              ^^^^ definition scip-sql . postgres . shop/order_status#paid.
--                                                   kind EnumMember
--                                                   display_name paid
--                                                   documentation
--                                                   > ```sql
--                                                   > 'paid' (shop.order_status)
--                                                   > ```
--                                                      ^^^^^^^ definition scip-sql . postgres . shop/order_status#shipped.
--                                                              kind EnumMember
--                                                              display_name shipped
--                                                              documentation
--                                                              > ```sql
--                                                              > 'shipped' (shop.order_status)
--                                                              > ```
--                                                              ⌃ enclosing_range_end scip-sql . postgres . shop/order_status#
  
  -- Customers of the shop.
--⌄ enclosing_range_start scip-sql . postgres . shop/customers#
  CREATE TABLE customers (
--             ^^^^^^^^^ definition scip-sql . postgres . shop/customers#
--                       kind Class
--                       display_name customers
--                       documentation
--                       > ```sql
--                       > TABLE shop.customers
--                       > ```
--                       documentation
--                       > Customers of the shop.
--    ⌄ enclosing_range_start scip-sql . postgres . shop/customers#id.
      id         serial PRIMARY KEY,
--    ^^ definition scip-sql . postgres . shop/customers#id.
--       kind Field
--       display_name id
--       documentation
--       > ```sql
--       > id serial PRIMARY KEY
--       > ```
--                                ⌃ enclosing_range_end scip-sql . postgres . shop/customers#id.
--    ⌄ enclosing_range_start scip-sql . postgres . shop/customers#email.
      email      text NOT NULL UNIQUE, -- login e-mail
--    ^^^^^ definition scip-sql . postgres . shop/customers#email.
--          kind Field
--          display_name email
--          documentation
--          > ```sql
--          > email text NOT NULL UNIQUE
--          > ```
--          documentation
--          > login e-mail
--                                  ⌃ enclosing_range_end scip-sql . postgres . shop/customers#email.
--    ⌄ enclosing_range_start scip-sql . postgres . shop/customers#full_name.
      full_name  varchar(200),
--    ^^^^^^^^^ definition scip-sql . postgres . shop/customers#full_name.
--              kind Field
--              display_name full_name
--              documentation
--              > ```sql
--              > full_name varchar(200)
--              > ```
--              documentation
--              > login e-mail
--                          ⌃ enclosing_range_end scip-sql . postgres . shop/customers#full_name.
--    ⌄ enclosing_range_start scip-sql . postgres . shop/customers#created_at.
      created_at timestamptz DEFAULT now()
--    ^^^^^^^^^^ definition scip-sql . postgres . shop/customers#created_at.
--               kind Field
--               display_name created_at
--               documentation
--               > ```sql
--               > created_at timestamptz DEFAULT now()
--               > ```
--                                       ⌃ enclosing_range_end scip-sql . postgres . shop/customers#created_at.
  );
--⌃ enclosing_range_end scip-sql . postgres . shop/customers#
  
--⌄ enclosing_range_start scip-sql . postgres . shop/orders#
  CREATE TABLE orders (
--             ^^^^^^ definition scip-sql . postgres . shop/orders#
--                    kind Class
--                    display_name orders
--                    documentation
--                    > ```sql
--                    > TABLE shop.orders
--                    > ```
--                    documentation
--                    > One row per checkout.
--    ⌄ enclosing_range_start scip-sql . postgres . shop/orders#id.
      id          bigserial PRIMARY KEY,
--    ^^ definition scip-sql . postgres . shop/orders#id.
--       kind Field
--       display_name id
--       documentation
--       > ```sql
--       > id bigserial PRIMARY KEY
--       > ```
--                                    ⌃ enclosing_range_end scip-sql . postgres . shop/orders#id.
--    ⌄ enclosing_range_start scip-sql . postgres . shop/orders#customer_id.
      customer_id integer NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
--    ^^^^^^^^^^^ definition scip-sql . postgres . shop/orders#customer_id.
--                kind Field
--                display_name customer_id
--                documentation
--                > ```sql
--                > customer_id integer NOT NULL REFERENCES customers (id) ON DELETE CASCADE
--                > ```
--                relationship scip-sql . postgres . shop/customers#id. reference
--                                            ^^^^^^^^^ reference scip-sql . postgres . shop/customers#
--                                                       ^^ reference scip-sql . postgres . shop/customers#id.
--                                                                           ⌃ enclosing_range_end scip-sql . postgres . shop/orders#customer_id.
--    ⌄ enclosing_range_start scip-sql . postgres . shop/orders#status.
      status      order_status NOT NULL DEFAULT 'pending',
--    ^^^^^^ definition scip-sql . postgres . shop/orders#status.
--           kind Field
--           display_name status
--           documentation
--           > ```sql
--           > status order_status NOT NULL DEFAULT 'pending'
--           > ```
--           relationship scip-sql . postgres . shop/order_status# type_definition
--                ^^^^^^^^^^^^ reference scip-sql . postgres . shop/order_status#
--                                                      ⌃ enclosing_range_end scip-sql . postgres . shop/orders#status.
--    ⌄ enclosing_range_start scip-sql . postgres . shop/orders#total.
      total       numeric(12, 2) CHECK (total >= 0),
--    ^^^^^ definition scip-sql . postgres . shop/orders#total.
--          kind Field
--          display_name total
--          documentation
--          > ```sql
--          > total numeric(12, 2) CHECK (total >= 0)
--          > ```
--          documentation
--          > Gross amount in EUR.
--                                      ^^^^^ reference scip-sql . postgres . shop/orders#total.
--                                                ⌃ enclosing_range_end scip-sql . postgres . shop/orders#total.
--    ⌄ enclosing_range_start scip-sql . postgres . shop/orders#orders_total_positive:
      CONSTRAINT orders_total_positive CHECK (total > 0)
--               ^^^^^^^^^^^^^^^^^^^^^ definition scip-sql . postgres . shop/orders#orders_total_positive:
--                                     kind Key
--                                     display_name orders_total_positive
--                                     documentation
--                                     > ```sql
--                                     > CONSTRAINT orders_total_positive CHECK (total > 0)
--                                     > ```
--                                            ^^^^^ reference scip-sql . postgres . shop/orders#total.
--                                                     ⌃ enclosing_range_end scip-sql . postgres . shop/orders#orders_total_positive:
  );
--⌃ enclosing_range_end scip-sql . postgres . shop/orders#
  
--⌄ enclosing_range_start scip-sql . postgres . shop/orders#orders_customer_idx:
  CREATE INDEX orders_customer_idx ON orders (customer_id, status);
--             ^^^^^^^^^^^^^^^^^^^ definition scip-sql . postgres . shop/orders#orders_customer_idx:
--                                 kind Key
--                                 display_name orders_customer_idx
--                                 documentation
--                                 > ```sql
--                                 > INDEX orders_customer_idx ON shop.orders
--                                 > ```
--                                    ^^^^^^ reference scip-sql . postgres . shop/orders#
--                                            ^^^^^^^^^^^ reference scip-sql . postgres . shop/orders#customer_id.
--                                                         ^^^^^^ reference scip-sql . postgres . shop/orders#status.
--                                                               ⌃ enclosing_range_end scip-sql . postgres . shop/orders#orders_customer_idx:
  
  COMMENT ON TABLE orders IS 'One row per checkout.';
--                 ^^^^^^ reference scip-sql . postgres . shop/orders#
  COMMENT ON COLUMN orders.total IS 'Gross amount in EUR.';
--                  ^^^^^^ reference scip-sql . postgres . shop/orders#
--                         ^^^^^ reference scip-sql . postgres . shop/orders#total.
  
