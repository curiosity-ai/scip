CREATE DATABASE IF NOT EXISTS blog;
USE blog;

CREATE TABLE `authors` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(100) NOT NULL COMMENT 'Pen name',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='People who write posts';

CREATE TABLE posts (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  author_id INT UNSIGNED NOT NULL,
  title VARCHAR(200) NOT NULL,
  published TINYINT(1) DEFAULT 0,
  KEY idx_author (author_id),
  CONSTRAINT fk_posts_author FOREIGN KEY (author_id) REFERENCES authors (id)
) ENGINE=InnoDB;

DELIMITER $$
CREATE PROCEDURE publish_all(IN p_author INT)
BEGIN
  DECLARE done INT DEFAULT 0;
  UPDATE posts SET published = 1 WHERE author_id = p_author;
  SELECT COUNT(*) INTO done FROM posts WHERE author_id = p_author AND published = 1;
END$$

CREATE TRIGGER posts_bi BEFORE INSERT ON posts FOR EACH ROW
BEGIN
  SET NEW.title = TRIM(NEW.title);
END$$
DELIMITER ;

SET @author := (SELECT id FROM authors WHERE name = 'ann');
CALL publish_all(@author);
INSERT INTO posts (author_id, title) VALUES (@author, 'Hello')
  ON DUPLICATE KEY UPDATE title = VALUES(title);
SELECT a.name, COUNT(p.id) AS n FROM authors a LEFT JOIN posts p ON p.author_id = a.id GROUP BY a.name;
ALTER TABLE posts CHANGE COLUMN title headline VARCHAR(250) NOT NULL, ADD COLUMN slug VARCHAR(250);
RENAME TABLE posts TO articles;
SELECT headline, slug FROM articles;
