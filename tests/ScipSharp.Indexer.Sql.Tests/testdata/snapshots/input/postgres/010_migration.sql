ALTER TABLE orders ADD COLUMN shipped_at timestamptz;
ALTER TABLE customers RENAME COLUMN full_name TO display_name;
UPDATE customers SET display_name = upper(display_name) WHERE id IN (SELECT customer_id FROM orders);
INSERT INTO orders (customer_id, status, total) VALUES (1, 'paid', 10.5)
ON CONFLICT (id) DO UPDATE SET total = EXCLUDED.total
RETURNING id, shipped_at;
DELETE FROM orders o USING customers c WHERE o.customer_id = c.id AND c.email LIKE '%@example.com';
SELECT * FROM unknown_table u WHERE u.flag;
