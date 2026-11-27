-- Shop schema (PostgreSQL)
CREATE SCHEMA shop;
SET search_path TO shop, public;

CREATE TYPE order_status AS ENUM ('pending', 'paid', 'shipped');

-- Customers of the shop.
CREATE TABLE customers (
    id         serial PRIMARY KEY,
    email      text NOT NULL UNIQUE, -- login e-mail
    full_name  varchar(200),
    created_at timestamptz DEFAULT now()
);

CREATE TABLE orders (
    id          bigserial PRIMARY KEY,
    customer_id integer NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
    status      order_status NOT NULL DEFAULT 'pending',
    total       numeric(12, 2) CHECK (total >= 0),
    CONSTRAINT orders_total_positive CHECK (total > 0)
);

CREATE INDEX orders_customer_idx ON orders (customer_id, status);

COMMENT ON TABLE orders IS 'One row per checkout.';
COMMENT ON COLUMN orders.total IS 'Gross amount in EUR.';
