  PRAGMA foreign_keys = ON;
--⌄ enclosing_range_start scip-sql . sqlite . main/notes#
  CREATE TABLE IF NOT EXISTS notes (
--                           ^^^^^ definition scip-sql . sqlite . main/notes#
--                                 kind Class
--                                 display_name notes
--                                 documentation
--                                 > ```sql
--                                 > TABLE main.notes
--                                 > ```
--  ⌄ enclosing_range_start scip-sql . sqlite . main/notes#id.
    id INTEGER PRIMARY KEY AUTOINCREMENT,
--  ^^ definition scip-sql . sqlite . main/notes#id.
--     kind Field
--     display_name id
--     documentation
--     > ```sql
--     > id INTEGER PRIMARY KEY AUTOINCREMENT
--     > ```
--                                     ⌃ enclosing_range_end scip-sql . sqlite . main/notes#id.
--  ⌄ enclosing_range_start scip-sql . sqlite . main/notes#body.
    body TEXT NOT NULL,
--  ^^^^ definition scip-sql . sqlite . main/notes#body.
--       kind Field
--       display_name body
--       documentation
--       > ```sql
--       > body TEXT NOT NULL
--       > ```
--                   ⌃ enclosing_range_end scip-sql . sqlite . main/notes#body.
--  ⌄ enclosing_range_start scip-sql . sqlite . main/notes#folder_id.
    folder_id INTEGER REFERENCES folders,
--  ^^^^^^^^^ definition scip-sql . sqlite . main/notes#folder_id.
--            kind Field
--            display_name folder_id
--            documentation
--            > ```sql
--            > folder_id INTEGER REFERENCES folders
--            > ```
--            relationship scip-sql . sqlite . main/folders#id. reference
--                               ^^^^^^^ reference scip-sql . sqlite . main/folders#
--                                     ⌃ enclosing_range_end scip-sql . sqlite . main/notes#folder_id.
--  ⌄ enclosing_range_start scip-sql . sqlite . main/notes#updated.
    updated
--  ^^^^^^^ definition scip-sql . sqlite . main/notes#updated.
--          kind Field
--          display_name updated
--          documentation
--          > ```sql
--          > updated
--          > ```
--        ⌃ enclosing_range_end scip-sql . sqlite . main/notes#updated.
  ) WITHOUT ROWID;
--              ⌃ enclosing_range_end scip-sql . sqlite . main/notes#
--⌄ enclosing_range_start scip-sql . sqlite . main/folders#
--                      ⌄ enclosing_range_start scip-sql . sqlite . main/folders#id.
--                                              ⌄ enclosing_range_start scip-sql . sqlite . main/folders#title.
  CREATE TABLE folders (id INTEGER PRIMARY KEY, title TEXT);
--             ^^^^^^^ definition scip-sql . sqlite . main/folders#
--                     kind Class
--                     display_name folders
--                     documentation
--                     > ```sql
--                     > TABLE main.folders
--                     > ```
--                      ^^ definition scip-sql . sqlite . main/folders#id.
--                         kind Field
--                         display_name id
--                         documentation
--                         > ```sql
--                         > id INTEGER PRIMARY KEY
--                         > ```
--                                              ^^^^^ definition scip-sql . sqlite . main/folders#title.
--                                                    kind Field
--                                                    display_name title
--                                                    documentation
--                                                    > ```sql
--                                                    > title TEXT
--                                                    > ```
--                                           ⌃ enclosing_range_end scip-sql . sqlite . main/folders#id.
--                                                       ⌃ enclosing_range_end scip-sql . sqlite . main/folders#title.
--                                                        ⌃ enclosing_range_end scip-sql . sqlite . main/folders#
--⌄ enclosing_range_start scip-sql . sqlite . main/notes#notes_touch:
  CREATE TRIGGER notes_touch AFTER UPDATE OF body ON notes
--               ^^^^^^^^^^^ definition scip-sql . sqlite . main/notes#notes_touch:
--                           kind Event
--                           display_name notes_touch
--                           documentation
--                           > ```sql
--                           > TRIGGER notes_touch ON main.notes
--                           > ```
--                                           ^^^^ reference scip-sql . sqlite . main/notes#body.
--                                                   ^^^^^ reference scip-sql . sqlite . main/notes#
  BEGIN
    UPDATE notes SET updated = datetime('now') WHERE id = NEW.id;
--         ^^^^^ reference scip-sql . sqlite . main/notes#
--                   ^^^^^^^ reference scip-sql . sqlite . main/notes#updated.
--                                                   ^^ reference scip-sql . sqlite . main/notes#id.
--                                                            ^^ reference scip-sql . sqlite . main/notes#id.
  END;
--  ⌃ enclosing_range_end scip-sql . sqlite . main/notes#notes_touch:
--⌄ enclosing_range_start scip-sql . sqlite . main/folder_counts#
  CREATE VIEW folder_counts AS SELECT f.title, count(*) AS n FROM folders f JOIN notes n ON n.folder_id = f.id GROUP BY f.title;
--            ^^^^^^^^^^^^^ definition scip-sql . sqlite . main/folder_counts#
--                          kind Class
--                          display_name folder_counts
--                          documentation
--                          > ```sql
--                          > VIEW main.folder_counts
--                          > ```
--                                    ^ reference local 0
--                                      ^^^^^ reference scip-sql . sqlite . main/folders#title.
--                                      ^^^^^ definition scip-sql . sqlite . main/folder_counts#title.
--                                            kind Field
--                                            display_name title
--                                            documentation
--                                            > ```sql
--                                            > title (column of main.folder_counts)
--                                            > ```
--                                            relationship scip-sql . sqlite . main/folders#title. reference
--                                                         ^ definition scip-sql . sqlite . main/folder_counts#n.
--                                                           kind Field
--                                                           display_name n
--                                                           documentation
--                                                           > ```sql
--                                                           > n (column of main.folder_counts)
--                                                           > ```
--                                                                ^^^^^^^ reference scip-sql . sqlite . main/folders#
--                                                                        ^ definition local 0
--                                                                          kind Variable
--                                                                          display_name f
--                                                                          documentation
--                                                                          > ```sql
--                                                                          > f: folders
--                                                                          > ```
--                                                                               ^^^^^ reference scip-sql . sqlite . main/notes#
--                                                                                     ^ definition local 1
--                                                                                       kind Variable
--                                                                                       display_name n
--                                                                                       documentation
--                                                                                       > ```sql
--                                                                                       > n: notes
--                                                                                       > ```
--                                                                                          ^ reference local 1
--                                                                                            ^^^^^^^^^ reference scip-sql . sqlite . main/notes#folder_id.
--                                                                                                        ^ reference local 0
--                                                                                                          ^^ reference scip-sql . sqlite . main/folders#id.
--                                                                                                                      ^ reference local 0
--                                                                                                                        ^^^^^ reference scip-sql . sqlite . main/folders#title.
--                                                                                                                            ⌃ enclosing_range_end scip-sql . sqlite . main/folder_counts#
  SELECT * FROM folder_counts WHERE n > 1;
--              ^^^^^^^^^^^^^ reference scip-sql . sqlite . main/folder_counts#
--                                  ^ reference scip-sql . sqlite . main/folder_counts#n.
  INSERT OR REPLACE INTO notes (id, body) VALUES (1, 'x');
--                       ^^^^^ reference scip-sql . sqlite . main/notes#
--                              ^^ reference scip-sql . sqlite . main/notes#id.
--                                  ^^^^ reference scip-sql . sqlite . main/notes#body.
  
