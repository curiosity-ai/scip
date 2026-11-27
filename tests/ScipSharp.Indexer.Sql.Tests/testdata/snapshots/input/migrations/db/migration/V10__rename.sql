ALTER TABLE users RENAME COLUMN name TO full_name;
ALTER TABLE users RENAME TO accounts;
DROP INDEX users_email_idx;
SELECT id, email, full_name FROM accounts;
