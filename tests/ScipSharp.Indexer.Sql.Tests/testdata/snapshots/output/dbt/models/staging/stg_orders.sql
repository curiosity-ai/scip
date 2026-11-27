  with source as (
--     ^^^^^^ definition local 0
--            kind Class
--            display_name source
--            documentation
--            > ```sql
--            > WITH source AS (...)
--            > ```
      select * from {{ source('raw', 'orders') }}
--                  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ reference scip-sql . dbt . raw/
--                  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ reference scip-sql . dbt . raw/orders#
  )
  select
      id as order_id,
--          ^^^^^^^^ definition scip-sql . dbt . default/stg_orders#order_id.
--                   kind Field
--                   display_name order_id
--                   documentation
--                   > ```sql
--                   > order_id (column of default.stg_orders)
--                   > ```
      user_id as customer_id,
--               ^^^^^^^^^^^ definition scip-sql . dbt . default/stg_orders#customer_id.
--                           kind Field
--                           display_name customer_id
--                           documentation
--                           > ```sql
--                           > customer_id (column of default.stg_orders)
--                           > ```
      order_date,
--    ^^^^^^^^^^ definition scip-sql . dbt . default/stg_orders#order_date.
--               kind Field
--               display_name order_date
--               documentation
--               > ```sql
--               > order_date (column of default.stg_orders)
--               > ```
      status
--    ^^^^^^ definition scip-sql . dbt . default/stg_orders#status.
--           kind Field
--           display_name status
--           documentation
--           > ```sql
--           > status (column of default.stg_orders)
--           > ```
  from source
--     ^^^^^^ reference local 0
  
