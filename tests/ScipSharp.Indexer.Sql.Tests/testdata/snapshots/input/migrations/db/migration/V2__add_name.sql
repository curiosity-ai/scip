ALTER TABLE users ADD COLUMN name text;
CREATE INDEX users_email_idx ON users (email);
