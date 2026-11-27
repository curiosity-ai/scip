--⌄ enclosing_range_start scip-sql . mysql . blog/
  CREATE DATABASE IF NOT EXISTS blog;
--                              ^^^^ definition scip-sql . mysql . blog/
--                                   kind Namespace
--                                   display_name blog
--                                   documentation
--                                   > ```sql
--                                   > SCHEMA blog
--                                   > ```
--                                 ⌃ enclosing_range_end scip-sql . mysql . blog/
  USE blog;
--    ^^^^ reference scip-sql . mysql . blog/
  
--⌄ enclosing_range_start scip-sql . mysql . blog/authors#
  CREATE TABLE `authors` (
--             ^^^^^^^^^ definition scip-sql . mysql . blog/authors#
--                       kind Class
--                       display_name authors
--                       documentation
--                       > ```sql
--                       > TABLE blog.authors
--                       > ```
--                       documentation
--                       > People who write posts
--  ⌄ enclosing_range_start scip-sql . mysql . blog/authors#id.
    `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
--  ^^^^ definition scip-sql . mysql . blog/authors#id.
--       kind Field
--       display_name id
--       documentation
--       > ```sql
--       > `id` INT UNSIGNED NOT NULL AUTO_INCREMENT
--       > ```
--                                          ⌃ enclosing_range_end scip-sql . mysql . blog/authors#id.
--  ⌄ enclosing_range_start scip-sql . mysql . blog/authors#name.
    `name` VARCHAR(100) NOT NULL COMMENT 'Pen name',
--  ^^^^^^ definition scip-sql . mysql . blog/authors#name.
--         kind Field
--         display_name name
--         documentation
--         > ```sql
--         > `name` VARCHAR(100) NOT NULL COMMENT 'Pen name'
--         > ```
--         documentation
--         > Pen name
--                                                ⌃ enclosing_range_end scip-sql . mysql . blog/authors#name.
    PRIMARY KEY (`id`),
--               ^^^^ reference scip-sql . mysql . blog/authors#id.
--  ⌄ enclosing_range_start scip-sql . mysql . blog/authors#uq_name:
    UNIQUE KEY `uq_name` (`name`)
--             ^^^^^^^^^ definition scip-sql . mysql . blog/authors#uq_name:
--                       kind Key
--                       display_name uq_name
--                       documentation
--                       > ```sql
--                       > UNIQUE KEY `uq_name` (`name`)
--                       > ```
--                        ^^^^^^ reference scip-sql . mysql . blog/authors#name.
--                              ⌃ enclosing_range_end scip-sql . mysql . blog/authors#uq_name:
  ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='People who write posts';
--                                                                       ⌃ enclosing_range_end scip-sql . mysql . blog/authors#
  
--⌄ enclosing_range_start scip-sql . mysql . blog/posts#
  CREATE TABLE posts (
--             ^^^^^ definition scip-sql . mysql . blog/posts#
--                   kind Class
--                   display_name posts
--                   documentation
--                   > ```sql
--                   > TABLE blog.posts
--                   > ```
--  ⌄ enclosing_range_start scip-sql . mysql . blog/posts#id.
    id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
--  ^^ definition scip-sql . mysql . blog/posts#id.
--     kind Field
--     display_name id
--     documentation
--     > ```sql
--     > id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY
--     > ```
--                                                    ⌃ enclosing_range_end scip-sql . mysql . blog/posts#id.
--  ⌄ enclosing_range_start scip-sql . mysql . blog/posts#author_id.
    author_id INT UNSIGNED NOT NULL,
--  ^^^^^^^^^ definition scip-sql . mysql . blog/posts#author_id.
--            kind Field
--            display_name author_id
--            documentation
--            > ```sql
--            > author_id INT UNSIGNED NOT NULL
--            > ```
--            relationship scip-sql . mysql . blog/authors#id. reference
--                                ⌃ enclosing_range_end scip-sql . mysql . blog/posts#author_id.
--  ⌄ enclosing_range_start scip-sql . mysql . blog/posts#title.
    title VARCHAR(200) NOT NULL,
--  ^^^^^ definition scip-sql . mysql . blog/posts#title.
--        kind Field
--        display_name title
--        documentation
--        > ```sql
--        > title VARCHAR(200) NOT NULL
--        > ```
--                            ⌃ enclosing_range_end scip-sql . mysql . blog/posts#title.
--  ⌄ enclosing_range_start scip-sql . mysql . blog/posts#published.
    published TINYINT(1) DEFAULT 0,
--  ^^^^^^^^^ definition scip-sql . mysql . blog/posts#published.
--            kind Field
--            display_name published
--            documentation
--            > ```sql
--            > published TINYINT(1) DEFAULT 0
--            > ```
--                               ⌃ enclosing_range_end scip-sql . mysql . blog/posts#published.
--  ⌄ enclosing_range_start scip-sql . mysql . blog/posts#idx_author:
    KEY idx_author (author_id),
--      ^^^^^^^^^^ definition scip-sql . mysql . blog/posts#idx_author:
--                 kind Key
--                 display_name idx_author
--                 documentation
--                 > ```sql
--                 > KEY idx_author (author_id)
--                 > ```
--                  ^^^^^^^^^ reference scip-sql . mysql . blog/posts#author_id.
--                           ⌃ enclosing_range_end scip-sql . mysql . blog/posts#idx_author:
--  ⌄ enclosing_range_start scip-sql . mysql . blog/posts#fk_posts_author:
    CONSTRAINT fk_posts_author FOREIGN KEY (author_id) REFERENCES authors (id)
--             ^^^^^^^^^^^^^^^ definition scip-sql . mysql . blog/posts#fk_posts_author:
--                             kind Key
--                             display_name fk_posts_author
--                             documentation
--                             > ```sql
--                             > CONSTRAINT fk_posts_author FOREIGN KEY (author_id) REFERENCES authors (id)
--                             > ```
--                                          ^^^^^^^^^ reference scip-sql . mysql . blog/posts#author_id.
--                                                                ^^^^^^^ reference scip-sql . mysql . blog/authors#
--                                                                         ^^ reference scip-sql . mysql . blog/authors#id.
--                                                                           ⌃ enclosing_range_end scip-sql . mysql . blog/posts#fk_posts_author:
  ) ENGINE=InnoDB;
--              ⌃ enclosing_range_end scip-sql . mysql . blog/posts#
  
  DELIMITER $$
--⌄ enclosing_range_start scip-sql . mysql . blog/publish_all().
  CREATE PROCEDURE publish_all(IN p_author INT)
--                 ^^^^^^^^^^^ definition scip-sql . mysql . blog/publish_all().
--                             kind Method
--                             display_name publish_all
--                             documentation
--                             > ```sql
--                             > PROCEDURE blog.publish_all(IN p_author INT)
--                             > ```
--                                ^^^^^^^^ definition scip-sql . mysql . blog/publish_all().(p_author)
--                                         kind Parameter
--                                         display_name p_author
--                                         documentation
--                                         > ```sql
--                                         > IN p_author INT
--                                         > ```
  BEGIN
--          ⌄ enclosing_range_start local 0
    DECLARE done INT DEFAULT 0;
--          ^^^^ definition local 0
--               kind Variable
--               display_name done
--               documentation
--               > ```sql
--               > DECLARE done INT
--               > ```
--                           ⌃ enclosing_range_end local 0
    UPDATE posts SET published = 1 WHERE author_id = p_author;
--         ^^^^^ reference scip-sql . mysql . blog/posts#
--                   ^^^^^^^^^ reference scip-sql . mysql . blog/posts#published.
--                                       ^^^^^^^^^ reference scip-sql . mysql . blog/posts#author_id.
--                                                   ^^^^^^^^ reference scip-sql . mysql . blog/publish_all().(p_author)
    SELECT COUNT(*) INTO done FROM posts WHERE author_id = p_author AND published = 1;
--                       ^^^^ reference local 0
--                                 ^^^^^ reference scip-sql . mysql . blog/posts#
--                                             ^^^^^^^^^ reference scip-sql . mysql . blog/posts#author_id.
--                                                         ^^^^^^^^ reference scip-sql . mysql . blog/publish_all().(p_author)
--                                                                      ^^^^^^^^^ reference scip-sql . mysql . blog/posts#published.
  END$$
--  ⌃ enclosing_range_end scip-sql . mysql . blog/publish_all().
  
--⌄ enclosing_range_start scip-sql . mysql . blog/posts#posts_bi:
  CREATE TRIGGER posts_bi BEFORE INSERT ON posts FOR EACH ROW
--               ^^^^^^^^ definition scip-sql . mysql . blog/posts#posts_bi:
--                        kind Event
--                        display_name posts_bi
--                        documentation
--                        > ```sql
--                        > TRIGGER posts_bi ON blog.posts
--                        > ```
--                                         ^^^^^ reference scip-sql . mysql . blog/posts#
  BEGIN
    SET NEW.title = TRIM(NEW.title);
--          ^^^^^ reference scip-sql . mysql . blog/posts#title.
--                           ^^^^^ reference scip-sql . mysql . blog/posts#title.
  END$$
--  ⌃ enclosing_range_end scip-sql . mysql . blog/posts#posts_bi:
  DELIMITER ;
  
  SET @author := (SELECT id FROM authors WHERE name = 'ann');
--    ^^^^^^^ definition local 1
--            kind Variable
--            display_name @author
--            documentation
--            > ```sql
--            > user variable @author
--            > ```
--                       ^^ reference scip-sql . mysql . blog/authors#id.
--                               ^^^^^^^ reference scip-sql . mysql . blog/authors#
--                                             ^^^^ reference scip-sql . mysql . blog/authors#name.
  CALL publish_all(@author);
--     ^^^^^^^^^^^ reference scip-sql . mysql . blog/publish_all().
--                 ^^^^^^^ reference local 1
  INSERT INTO posts (author_id, title) VALUES (@author, 'Hello')
--            ^^^^^ reference scip-sql . mysql . blog/posts#
--                   ^^^^^^^^^ reference scip-sql . mysql . blog/posts#author_id.
--                              ^^^^^ reference scip-sql . mysql . blog/posts#title.
--                                             ^^^^^^^ reference local 1
    ON DUPLICATE KEY UPDATE title = VALUES(title);
--                          ^^^^^ reference scip-sql . mysql . blog/posts#title.
--                                         ^^^^^ reference scip-sql . mysql . blog/posts#title.
  SELECT a.name, COUNT(p.id) AS n FROM authors a LEFT JOIN posts p ON p.author_id = a.id GROUP BY a.name;
--       ^ reference local 2
--         ^^^^ reference scip-sql . mysql . blog/authors#name.
--                     ^ reference local 3
--                       ^^ reference scip-sql . mysql . blog/posts#id.
--                              ^ definition local 4
--                                kind Variable
--                                display_name n
--                                documentation
--                                > ```sql
--                                > column alias n
--                                > ```
--                                     ^^^^^^^ reference scip-sql . mysql . blog/authors#
--                                             ^ definition local 2
--                                               kind Variable
--                                               display_name a
--                                               documentation
--                                               > ```sql
--                                               > a: authors
--                                               > ```
--                                                         ^^^^^ reference scip-sql . mysql . blog/posts#
--                                                               ^ definition local 3
--                                                                 kind Variable
--                                                                 display_name p
--                                                                 documentation
--                                                                 > ```sql
--                                                                 > p: posts
--                                                                 > ```
--                                                                    ^ reference local 3
--                                                                      ^^^^^^^^^ reference scip-sql . mysql . blog/posts#author_id.
--                                                                                  ^ reference local 2
--                                                                                    ^^ reference scip-sql . mysql . blog/authors#id.
--                                                                                                ^ reference local 2
--                                                                                                  ^^^^ reference scip-sql . mysql . blog/authors#name.
--                  ⌄ enclosing_range_start scip-sql . mysql . blog/posts#headline.
--                                                                                 ⌄ enclosing_range_start scip-sql . mysql . blog/posts#slug.
  ALTER TABLE posts CHANGE COLUMN title headline VARCHAR(250) NOT NULL, ADD COLUMN slug VARCHAR(250);
--            ^^^^^ reference scip-sql . mysql . blog/posts#
--                                ^^^^^ reference scip-sql . mysql . blog/posts#title.
--                                      ^^^^^^^^ definition scip-sql . mysql . blog/posts#headline.
--                                               kind Field
--                                               display_name headline
--                                               documentation
--                                               > ```sql
--                                               > headline VARCHAR(250) NOT NULL
--                                               > ```
--                                               documentation
--                                               > Renamed from title.
--                                               relationship scip-sql . mysql . blog/posts#title. reference
--                                                                                 ^^^^ definition scip-sql . mysql . blog/posts#slug.
--                                                                                      kind Field
--                                                                                      display_name slug
--                                                                                      documentation
--                                                                                      > ```sql
--                                                                                      > slug VARCHAR(250)
--                                                                                      > ```
--                                                                   ⌃ enclosing_range_end scip-sql . mysql . blog/posts#headline.
--                                                                                                 ⌃ enclosing_range_end scip-sql . mysql . blog/posts#slug.
--⌄ enclosing_range_start scip-sql . mysql . blog/articles#
  RENAME TABLE posts TO articles;
--             ^^^^^ reference scip-sql . mysql . blog/posts#
--                      ^^^^^^^^ definition scip-sql . mysql . blog/articles#
--                               kind Class
--                               display_name articles
--                               documentation
--                               > ```sql
--                               > TABLE blog.articles
--                               > ```
--                               documentation
--                               > Renamed from blog.posts.
--                               relationship scip-sql . mysql . blog/posts# reference
--                             ⌃ enclosing_range_end scip-sql . mysql . blog/articles#
  SELECT headline, slug FROM articles;
--       ^^^^^^^^ reference scip-sql . mysql . blog/posts#headline.
--                 ^^^^ reference scip-sql . mysql . blog/posts#slug.
--                           ^^^^^^^^ reference scip-sql . mysql . blog/articles#
  
