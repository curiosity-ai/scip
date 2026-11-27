# ScipSharp.Indexer.Sql

A native C# SCIP indexer for SQL: ANSI, PostgreSQL (incl. PL/pgSQL), T-SQL, MySQL/MariaDB, SQLite and Oracle (PL/SQL).
Run it with `scip index sql`. It needs no database server, `psql`, `sqlfluff` or any other external tool: lexing,
statement splitting, parsing and name resolution are all implemented here. There is no upstream `scip-sql` project
to port, so this is an original design.

```bash
scip index sql --project-root db/ --output index.scip
scip index sql --dialect=tsql --default-schema=dbo --package-name=warehouse
scip lint index.scip
```

## Options

| Option | Meaning |
|---|---|
| `--dialect=<d>` | `ansi`, `postgres` (`pg`, `redshift`), `tsql` (`mssql`, `sqlserver`), `mysql` (`mariadb`), `sqlite`, `plsql` (`oracle`). The default is to detect the dialect per file, falling back to a guess for the whole project when a file shows no clear signs. |
| `--default-schema=<s>` | The schema that unqualified names resolve to. The default depends on the dialect: `public`, `dbo`, `main`, or `default`. |
| `--package-name=<n>` | The package name in symbols. The default is the project directory's name. |
| `--package-version=<v>` | The package version in symbols. The default is empty. |
| `-h`, `--help` | Prints usage. |

## How it works

1. **Discovery.** The indexer reads every `.sql` file below the root (also `.ddl`, `.pks`, `.pkb`, `.pls`, `.prc`,
   `.fnc`, `.trg`, `.pgsql`, …). It skips hidden directories and `node_modules`, `target`, `dbt_packages` and
   `bin`/`obj`. Files are taken in **natural sort order** (`V2__x.sql` before `V10__y.sql`) so that migration
   directories (Flyway, Liquibase SQL, Rails/Django dumps, `NNN_*.sql`) replay correctly.
2. **Lexing** (`Lexing/SqlLexer.cs`) is dialect-aware:
   - Quoted identifiers: `"x"`, `[x]` (T-SQL, SQLite), `` `x` `` (MySQL, SQLite).
   - Strings: `''` escapes, `E''`, `N''`, `X''`, `B''`, `U&''`, Oracle `q'[..]'`, PostgreSQL `$tag$..$tag$`, MySQL
     backslash escapes, and MySQL double-quoted strings.
   - Comments: nested `/* */` (PostgreSQL, T-SQL), `--`, and MySQL `#`.
   - Separators and directives: T-SQL `GO [n]`, SQL\*Plus `/`, and MySQL `DELIMITER`. psql `\` meta-commands and
     SQL\*Plus commands are skipped.
   - Templates: dbt/Jinja `{{ ref() }}`/`{{ source() }}` become table references. `{% %}` control blocks and
     `{# #}` comments are skipped, so both branches of an `{% if %}` are indexed.
3. **Splitting** (`Lexing/StatementSplitter.cs`) cuts at `;` only outside parentheses and procedural blocks
   (`BEGIN..END`, `CASE..END`, PL/SQL `IS|AS … BEGIN … END` declaration sections). `GO`, `/` and custom delimiters
   always split. A PostgreSQL top-level `BEGIN;` is treated as a transaction. T-SQL scripts without semicolons are
   handled by the parser, which stops statements at statement-starting keywords.
4. **Parsing** (`Syntax/`) uses a tolerant recursive-descent parser that never throws. It keeps only what matters for
   names, in a flat AST:
   - DDL: `CREATE`, `ALTER`, `DROP` and `COMMENT ON` for schemas and databases, tables (columns, constraints,
     `INHERITS`, `LIKE`, `PARTITION OF`, CTAS), views and materialized views, indexes, sequences, types (enum,
     composite, Oracle object/collection, T-SQL table/alias types), domains, functions and procedures, triggers, and
     PL/SQL packages (spec and body).
   - Rename statements: `ALTER TABLE ADD/DROP/RENAME/ALTER/MODIFY/CHANGE COLUMN`, `RENAME TABLE`,
     `ALTER … RENAME TO`, `SET SCHEMA`, and `ALTER TYPE … ADD VALUE`.
   - DML: `SELECT` with joins, subqueries, CTEs (including `RECURSIVE` and data-modifying CTEs), set operations,
     windows, `LATERAL`/`APPLY`, `PIVOT`, `VALUES` and `TABLE t`. Also `INSERT`, `UPDATE`, `DELETE` and `MERGE` with
     `ON CONFLICT`, `ON DUPLICATE KEY`, `RETURNING` and `OUTPUT`, and the T-SQL/MySQL multi-table forms.
   - Procedural code: T-SQL batches and `@vars`; PL/pgSQL bodies (dollar-quoted bodies are re-lexed in place, so
     positions stay exact); PL/SQL blocks, nested routines, cursors, `%TYPE`/`%ROWTYPE` and cursor `FOR` loops; MySQL
     routines, handlers and `@user` variables; and `DO` blocks.
   - Anything the parser does not understand is skipped, but any expressions inside it are still resolved.
5. **Binding** (`Semantics/Binder*.cs`) runs two passes over the whole project.
   - The **catalog pass** replays all DDL. It records the final state of every object, together with `COMMENT ON`
     texts, relationships, and which tables use which trigger functions.
   - The **emitting pass** replays everything again in order and writes one document per file.
   - A reference resolves against the catalog as it stands at that statement. When the name is missing there, it
     falls back to the final catalog and then to every object ever created. This covers `pg_dump`-style forward
     references (functions written before their tables) and older code that uses columns renamed later.
   - Scopes follow SQL. FROM sources are tables, views, CTEs, derived tables, table functions and table variables.
     Aliases shadow table names. Correlated subqueries see the sources of outer scopes. ORDER BY and GROUP BY can
     use select-list aliases. `t.c`, `s.t.c` and `db.s.t.c` are resolved.
   - An unqualified column takes the first source in scope that has it. If no source has it, procedural variables
     and parameters are tried, then select aliases. Last, a column whose only possible source has an unknown column
     list (an unresolved table, or `SELECT *` from one) is attributed to that source.
   - `SELECT *` and `t.*` expand to the source's columns when they are known.
   - In triggers, `NEW`/`OLD`, `:NEW`/`:OLD`, T-SQL `inserted`/`deleted`, PostgreSQL `excluded` and `REFERENCING`
     aliases resolve to the trigger table. A PL/pgSQL `RETURNS trigger` function gets the table of the trigger that
     executes it.

## Symbol scheme

```
scip-sql . <package> <version> [<database>/]<schema>/<object>#<member>.
```

| Object | Descriptor | Example |
|---|---|---|
| database (T-SQL `USE`/`CREATE DATABASE`) | namespace | `Shop/` (a prefix of everything created after `USE Shop`) |
| schema (MySQL databases are schemas) | namespace | `public/` |
| table, view, materialized view, sequence, type, domain, dbt model | type | `public/orders#` |
| column, view output column, composite-type attribute | term | `public/orders#customer_id.` |
| enum label | term | `public/mood#happy.` |
| named constraint, index, trigger (owned by their table) | meta | `public/orders#orders_pkey:` |
| function or procedure (overloads disambiguated by normalized `IN` parameter types in PostgreSQL, PL/SQL and ANSI) | method | ``public/area(`integer,integer`).`` |
| parameter, or `RETURNS TABLE` column | parameter | `public/area(...).(w)` |
| PL/SQL package, with its members | namespace | `hr/emp_pkg/`, `hr/emp_pkg/raise(number).`, `hr/emp_pkg/g_rate.` |

- **Case folding.** Names are case-folded the way each dialect does it. PostgreSQL folds unquoted names to lower case
  and keeps quoted names exact. Oracle folds to upper case. The other dialects compare case-insensitively, so their
  keys are lower case. The definition's spelling is kept as `display_name`.
- **Renames.** Rename-in-place keeps column identities: after `ALTER TABLE users RENAME TO accounts`,
  `accounts.email` still resolves to `users#email.`. The new table symbol has a `reference` relationship to the old
  one. A renamed column gets a new symbol, with a relationship to the old one.
- **Locals.** These are `local N` symbols, each with SymbolInformation: table and derived-table aliases, CTEs and their
  columns, select-list aliases outside views and CTAS, procedural variables, cursors, loop variables, and MySQL
  `@user` variables (scoped to the file). Variables are scoped per T-SQL batch, or per top-level statement or block
  in the other dialects.
- **Unresolved names.** Tables and columns that are not defined anywhere in the project get ordinary symbols in the
  current or given schema, such as `public/missing#col.`. These are emitted as `external_symbols`. Built-in functions
  and unresolvable routine calls produce no occurrence.

Each definition gets the following:

- **Documentation:** a code block (the column definition text, `TABLE s.t`, or the routine signature), the `--` or
  `/* */` comments directly above the statement or column, a trailing `-- comment` on the column's line, MySQL
  `COMMENT '…'`, and `COMMENT ON` text (even from a later file).
- **Kind:** Class (tables and views), Field, Namespace, Type, Enum/EnumMember, TypeAlias, Function, Method, Parameter,
  Event (triggers), Key (indexes and constraints), Package, or Variable.
- **Relationships:**
  - An FK column has a `reference` to the referenced column (the PK when no columns are given).
  - A view, CTAS or dbt-model column has a `reference` to its source column.
  - A column with a user-defined type has a `type_definition` to the type.
  - Renames have a `reference` to the old symbol.
- **Enclosing range:** the statement, or the column definition.

Every document sets `position_encoding = UTF16CodeUnitOffsetFromLineStart`, and files are read as UTF-8, falling back
to Latin-1.

## dbt

If `dbt_project.yml` exists at the root, every file under a `models/` directory defines a relation named after the
file. Its columns come from the model's top-level `SELECT`. `{{ ref('m') }}` resolves to that relation, and
`{{ source('s', 't') }}` resolves to the table `s/t#`.

## Known gaps

- **No expression typing.** Overload resolution goes by argument count only, and composite field access such as
  `(rec).field` or `col.field` is not resolved.
- **Ambiguous unqualified columns** resolve to the first source in FROM order instead of being reported.
- **Search paths are per file.** `SET search_path` and `USE` apply from that point to the end of the file; session
  state does not carry over between files.
- **Dynamic SQL is not analysed:** `EXECUTE 'sql'`, `sp_executesql`, `EXECUTE IMMEDIATE`.
- **Unsupported statements and object kinds:**
  - T-SQL `sp_rename`, synonyms, and Oracle `CREATE TYPE BODY` and compound triggers. These are skipped.
  - MySQL events, rules, policies and roles, and `GRANT` on routines. These produce no symbols.
- **PL/SQL nested blocks** share one variable scope per routine, so shadowing inside inner `DECLARE` blocks is not
  modelled.
- **Partial dbt support:** no Jinja evaluation, macros or `{{ this }}`, and every model goes into the default schema.
- **No per-object dialect switching.** A dialect is chosen per file, so a project that mixes dialects with different
  case folding may resolve inconsistently across files. Use `--dialect` in that case.

## Tests

The tests are in `tests/ScipSharp.Indexer.Sql.Tests`:

- **Unit tests:** lexer per dialect, statement splitting, case folding, alias, CTE and correlated-subquery resolution,
  overloads, forward references, migration ordering with `ALTER`/`RENAME`/`DROP`, dbt, the CLI, and dialect
  detection.
- **Snapshot tests:** golden snapshots of multi-dialect fixtures in `testdata/snapshots`. To regenerate them, run
  `SCIP_UPDATE_SNAPSHOTS=1 dotnet test --project tests/ScipSharp.Indexer.Sql.Tests`.
- **Lint check:** every fixture must produce a lint-clean index.
- **Robustness corpus:** about 1,080 files from the sqlfluff dialect fixtures (MIT) and the PostgreSQL regression
  tests (PostgreSQL Licence). The test requires no errors and a lint-clean index. Origins are in
  `testdata/SOURCES.md`.
- **Optional `sqlite3` cross-check:** compares the tables and columns that `sqlite3` creates with the definitions the
  indexer emits. It is skipped when `sqlite3` is not installed.

## Research references

- **[sqlglot](https://github.com/tobymao/sqlglot) (MIT):** the main design reference for column resolution (the
  `optimizer/scope.py` and `qualify_columns.py` modules). Ideas taken from it include scope trees with
  sources and selected sources, attributing unqualified columns to the sole source, star expansion from a schema,
  and lineage from select-list aliases to source columns.
- **[sqlfluff](https://github.com/sqlfluff/sqlfluff) (MIT):** its per-dialect parser fixtures were the main corpus for
  lexer and parser robustness. Its dialect grammars were a checklist for keyword and statement coverage.
- **PostgreSQL [`src/test/regress/sql`](https://github.com/postgres/postgres/tree/master/src/test/regress/sql)
  (PostgreSQL Licence):** a corpus for DDL, `ALTER TABLE`, PL/pgSQL and triggers.
  [libpg_query](https://github.com/pganalyze/libpg_query) (BSD-3) was considered for exactness but rejected: it is
  native C and covers only PostgreSQL.
- **[ANTLR grammars-v4](https://github.com/antlr/grammars-v4)** (tsql, plsql, postgresql, mysql), JSqlParser and
  Calcite: grammar references for T-SQL and PL/SQL corner cases (batch-scoped procedures, package specs and bodies,
  `%TYPE`). No code was copied; JSqlParser is dual Apache-2.0/LGPL.
- **[tree-sitter-sql](https://github.com/DerekStride/tree-sitter-sql) (MIT), sqls and sql-language-server (MIT), and
  dbt-language-server:** their editor-oriented approach (tolerant parsing, alias-aware completion) confirmed the choice
  of a tolerant parser over a strict one. dbt-language-server showed how dbt `ref`/`source` resolution works.
- **[psql-scip](https://github.com/gnufood/psql-scip):** the only existing SCIP indexer for SQL found (Rust,
  PostgreSQL only, based on `pg_dump` schemas).
- **Other public corpora (not vendored):** Microsoft sql-server-samples (MIT), Oracle db-sample-schemas (MIT), dbt
  jaffle_shop (Apache-2.0), Sakila (BSD) and Chinook (MIT) are good larger real-world inputs for manual runs.
