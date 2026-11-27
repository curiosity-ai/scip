# Test data sources

| Directory | Origin | Commit | Licence |
|---|---|---|---|
| `snapshots/input/**` | Written for this project | — | Same as this repository |
| `corpus/sqlfluff/{ansi,postgres,tsql,mysql,sqlite,oracle}/*.sql` | https://github.com/sqlfluff/sqlfluff — `test/fixtures/dialects/<dialect>/*.sql` (unmodified) | `63fd396b5c0af320810724bbc4a5d3327d033093` | MIT (see `corpus/sqlfluff/LICENSE.md`) |
| `corpus/postgres-regress/*.sql` | https://github.com/postgres/postgres — `src/test/regress/sql/{alter_table,create_table,create_view,foreign_key,plpgsql,triggers,with}.sql` (unmodified) | `ca6c0383754bd1907c5b8fb3d08dd2ce2d148702` | PostgreSQL Licence (see `corpus/postgres-regress/COPYRIGHT`) |

The corpus files are only used by the robustness test (`CorpusTests`): every file must index without errors and the
resulting index must be lint-clean. Their output is not snapshotted.
