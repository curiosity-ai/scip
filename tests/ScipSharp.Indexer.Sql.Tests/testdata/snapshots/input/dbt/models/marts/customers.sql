{{ config(materialized='table') }}
with orders as (
    select customer_id, min(order_date) as first_order, count(*) as number_of_orders
    from {{ ref('stg_orders') }}
    {% if is_incremental() %} where order_date > '2020-01-01' {% endif %}
    group by customer_id
)
select c.customer_id, c.first_name, o.first_order, o.number_of_orders
from {{ ref('stg_customers') }} c
left join orders o on o.customer_id = c.customer_id
