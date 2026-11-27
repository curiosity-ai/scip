--                  ⌄ enclosing_range_start scip-sql . migrations . default/users#full_name.
  ALTER TABLE users RENAME COLUMN name TO full_name;
--            ^^^^^ reference scip-sql . migrations . default/users#
--                                ^^^^ reference scip-sql . migrations . default/users#name.
--                                        ^^^^^^^^^ definition scip-sql . migrations . default/users#full_name.
--                                                  kind Field
--                                                  display_name full_name
--                                                  documentation
--                                                  > ```sql
--                                                  > RENAME COLUMN name TO full_name
--                                                  > ```
--                                                  documentation
--                                                  > Renamed from name.
--                                                  relationship scip-sql . migrations . default/users#name. reference
--                                                ⌃ enclosing_range_end scip-sql . migrations . default/users#full_name.
--⌄ enclosing_range_start scip-sql . migrations . default/accounts#
  ALTER TABLE users RENAME TO accounts;
--            ^^^^^ reference scip-sql . migrations . default/users#
--                            ^^^^^^^^ definition scip-sql . migrations . default/accounts#
--                                     kind Class
--                                     display_name accounts
--                                     documentation
--                                     > ```sql
--                                     > TABLE default.accounts
--                                     > ```
--                                     documentation
--                                     > Renamed from default.users.
--                                     relationship scip-sql . migrations . default/users# reference
--                                   ⌃ enclosing_range_end scip-sql . migrations . default/accounts#
  DROP INDEX users_email_idx;
--           ^^^^^^^^^^^^^^^ reference scip-sql . migrations . default/users#users_email_idx:
  SELECT id, email, full_name FROM accounts;
--       ^^ reference scip-sql . migrations . default/users#id.
--           ^^^^^ reference scip-sql . migrations . default/users#email.
--                  ^^^^^^^^^ reference scip-sql . migrations . default/users#full_name.
--                                 ^^^^^^^^ reference scip-sql . migrations . default/accounts#
  
