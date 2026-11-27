-- runs before V10 thanks to natural ordering
UPDATE users SET name = split_part(email, '@', 1) WHERE name IS NULL;
