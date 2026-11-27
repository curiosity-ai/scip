CREATE VIEW shop.customer_totals AS
SELECT c.id AS customer_id, c.email, sum(o.total) AS lifetime_total
FROM shop.customers c
JOIN shop.orders o ON o.customer_id = c.id
WHERE o.status <> 'pending'
GROUP BY c.id, c.email;

WITH recent AS (
    SELECT customer_id, max(id) AS last_order
    FROM orders
    GROUP BY customer_id
)
SELECT r.customer_id, r.last_order, ct.lifetime_total
FROM recent r
LEFT JOIN shop.customer_totals ct USING (customer_id)
WHERE EXISTS (SELECT 1 FROM orders o2 WHERE o2.customer_id = r.customer_id AND o2.total > 100)
ORDER BY last_order DESC;
