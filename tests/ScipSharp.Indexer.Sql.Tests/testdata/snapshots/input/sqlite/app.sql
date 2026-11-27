PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS notes (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  body TEXT NOT NULL,
  folder_id INTEGER REFERENCES folders,
  updated
) WITHOUT ROWID;
CREATE TABLE folders (id INTEGER PRIMARY KEY, title TEXT);
CREATE TRIGGER notes_touch AFTER UPDATE OF body ON notes
BEGIN
  UPDATE notes SET updated = datetime('now') WHERE id = NEW.id;
END;
CREATE VIEW folder_counts AS SELECT f.title, count(*) AS n FROM folders f JOIN notes n ON n.folder_id = f.id GROUP BY f.title;
SELECT * FROM folder_counts WHERE n > 1;
INSERT OR REPLACE INTO notes (id, body) VALUES (1, 'x');
