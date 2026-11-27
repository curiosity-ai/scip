  -- runs before V10 thanks to natural ordering
  UPDATE users SET name = split_part(email, '@', 1) WHERE name IS NULL;
--       ^^^^^ reference scip-sql . migrations . default/users#
--                 ^^^^ reference scip-sql . migrations . default/users#name.
--                                   ^^^^^ reference scip-sql . migrations . default/users#email.
--                                                        ^^^^ reference scip-sql . migrations . default/users#name.
  
