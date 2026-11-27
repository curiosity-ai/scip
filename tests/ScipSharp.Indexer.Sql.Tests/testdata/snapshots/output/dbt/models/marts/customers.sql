  {{ config(materialized='table') }}
  with orders as (
--     ^^^^^^ definition local 0
--            kind Class
--            display_name orders
--            documentation
--            > ```sql
--            > WITH orders AS (...)
--            > ```
      select customer_id, min(order_date) as first_order, count(*) as number_of_orders
--           ^^^^^^^^^^^ reference scip-sql . dbt . default/stg_orders#customer_id.
--                            ^^^^^^^^^^ reference scip-sql . dbt . default/stg_orders#order_date.
--                                           ^^^^^^^^^^^ definition local 1
--                                                       kind Variable
--                                                       display_name first_order
--                                                       documentation
--                                                       > ```sql
--                                                       > column alias first_order
--                                                       > ```
--                                                                    ^^^^^^^^^^^^^^^^ definition local 2
--                                                                                     kind Variable
--                                                                                     display_name number_of_orders
--                                                                                     documentation
--                                                                                     > ```sql
--                                                                                     > column alias number_of_orders
--                                                                                     > ```
      from {{ ref('stg_orders') }}
--         ^^^^^^^^^^^^^^^^^^^^^^^ reference scip-sql . dbt . default/stg_orders#
      {% if is_incremental() %} where order_date > '2020-01-01' {% endif %}
--                                    ^^^^^^^^^^ reference scip-sql . dbt . default/stg_orders#order_date.
      group by customer_id
--             ^^^^^^^^^^^ reference scip-sql . dbt . default/stg_orders#customer_id.
  )
  select c.customer_id, c.first_name, o.first_order, o.number_of_orders
--       ^ reference local 3
--         ^^^^^^^^^^^ reference scip-sql . dbt . default/stg_customers#customer_id.
--                      ^ reference local 3
--                        ^^^^^^^^^^ reference scip-sql . dbt . default/stg_customers#first_name.
--                                    ^ reference local 4
--                                      ^^^^^^^^^^^ reference local 1
--                                                   ^ reference local 4
--                                                     ^^^^^^^^^^^^^^^^ reference local 2
  from {{ ref('stg_customers') }} c
--     ^^^^^^^^^^^^^^^^^^^^^^^^^^ reference scip-sql . dbt . default/stg_customers#
--                                ^ definition local 3
--                                  kind Variable
--                                  display_name c
--                                  documentation
--                                  > ```sql
--                                  > c: stg_customers
--                                  > ```
  left join orders o on o.customer_id = c.customer_id
--          ^^^^^^ reference local 0
--                 ^ definition local 4
--                   kind Variable
--                   display_name o
--                   documentation
--                   > ```sql
--                   > o: WITH orders
--                   > ```
--                      ^ reference local 4
--                        ^^^^^^^^^^^ reference scip-sql . dbt . default/stg_orders#customer_id.
--                                      ^ reference local 3
--                                        ^^^^^^^^^^^ reference scip-sql . dbt . default/stg_customers#customer_id.
  
