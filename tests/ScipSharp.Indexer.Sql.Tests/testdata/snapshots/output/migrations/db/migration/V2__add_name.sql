--                             ⌄ enclosing_range_start scip-sql . migrations . default/users#name.
  ALTER TABLE users ADD COLUMN name text;
--            ^^^^^ reference scip-sql . migrations . default/users#
--                             ^^^^ definition scip-sql . migrations . default/users#name.
--                                  kind Field
--                                  display_name name
--                                  documentation
--                                  > ```sql
--                                  > name text
--                                  > ```
--                                     ⌃ enclosing_range_end scip-sql . migrations . default/users#name.
--⌄ enclosing_range_start scip-sql . migrations . default/users#users_email_idx:
  CREATE INDEX users_email_idx ON users (email);
--             ^^^^^^^^^^^^^^^ definition scip-sql . migrations . default/users#users_email_idx:
--                             kind Key
--                             display_name users_email_idx
--                             documentation
--                             > ```sql
--                             > INDEX users_email_idx ON default.users
--                             > ```
--                                ^^^^^ reference scip-sql . migrations . default/users#
--                                       ^^^^^ reference scip-sql . migrations . default/users#email.
--                                            ⌃ enclosing_range_end scip-sql . migrations . default/users#users_email_idx:
  
