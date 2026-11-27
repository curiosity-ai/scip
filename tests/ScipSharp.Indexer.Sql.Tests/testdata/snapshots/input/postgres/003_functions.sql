CREATE OR REPLACE FUNCTION shop.order_count(p_customer integer)
RETURNS integer
LANGUAGE plpgsql AS $$
DECLARE
    v_count integer := 0;
BEGIN
    SELECT count(*) INTO v_count FROM shop.orders WHERE customer_id = p_customer;
    RETURN v_count;
END;
$$;

CREATE FUNCTION shop.touch() RETURNS trigger AS $$
BEGIN
    NEW.created_at := now();
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER customers_touch BEFORE INSERT ON shop.customers
FOR EACH ROW EXECUTE FUNCTION shop.touch();

SELECT shop.order_count(id) FROM customers;
