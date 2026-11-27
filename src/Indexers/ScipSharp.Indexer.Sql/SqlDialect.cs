using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Sql;

/// <summary>SQL dialects understood by the indexer. <see cref="Ansi"/> is the tolerant common denominator.</summary>
public enum SqlDialect
{
    Ansi,
    Postgres,
    TSql,
    MySql,
    Sqlite,
    PlSql,
}

/// <summary>Per-dialect lexical and naming rules.</summary>
public static partial class Dialects
{
    /// <summary>Parses a <c>--dialect</c> value (several common aliases are accepted).</summary>
    public static bool TryParse(string value, out SqlDialect dialect)
    {
        switch (value.ToLowerInvariant())
        {
            case "ansi" or "sql" or "generic":
                dialect = SqlDialect.Ansi;
                return true;
            case "postgres" or "postgresql" or "pg" or "plpgsql" or "redshift" or "greenplum":
                dialect = SqlDialect.Postgres;
                return true;
            case "tsql" or "t-sql" or "mssql" or "sqlserver" or "azuresql" or "sybase":
                dialect = SqlDialect.TSql;
                return true;
            case "mysql" or "mariadb":
                dialect = SqlDialect.MySql;
                return true;
            case "sqlite" or "sqlite3":
                dialect = SqlDialect.Sqlite;
                return true;
            case "plsql" or "pl/sql" or "oracle":
                dialect = SqlDialect.PlSql;
                return true;
            default:
                dialect = SqlDialect.Ansi;
                return false;
        }
    }

    public static string Name(this SqlDialect d) => d switch
    {
        SqlDialect.Postgres => "postgres",
        SqlDialect.TSql => "tsql",
        SqlDialect.MySql => "mysql",
        SqlDialect.Sqlite => "sqlite",
        SqlDialect.PlSql => "plsql",
        _ => "ansi",
    };

    /// <summary>Schema that unqualified names resolve to when nothing else (search path, USE, option) says otherwise.</summary>
    public static string DefaultSchema(this SqlDialect d) => d switch
    {
        SqlDialect.Postgres => "public",
        SqlDialect.TSql => "dbo",
        SqlDialect.Sqlite => "main",
        _ => "default",
    };

    /// <summary>
    /// Identity key of an identifier: PostgreSQL folds unquoted names to lower case and keeps quoted ones verbatim,
    /// Oracle folds unquoted names to upper case, the other dialects compare case-insensitively (keyed in lower case).
    /// </summary>
    public static string Normalize(this SqlDialect d, string name, bool quoted) => d switch
    {
        SqlDialect.Postgres => quoted ? name : name.ToLowerInvariant(),
        SqlDialect.PlSql => quoted ? name : name.ToUpperInvariant(),
        _ => name.ToLowerInvariant(),
    };

    public static bool NestedBlockComments(this SqlDialect d) => d is SqlDialect.Postgres or SqlDialect.TSql;

    public static bool HashComments(this SqlDialect d) => d is SqlDialect.MySql;

    public static bool BracketIdentifiers(this SqlDialect d) => d is SqlDialect.TSql or SqlDialect.Sqlite;

    public static bool BacktickIdentifiers(this SqlDialect d) => d is SqlDialect.MySql or SqlDialect.Sqlite or SqlDialect.Ansi;

    /// <summary>MySQL (without ANSI_QUOTES) treats double-quoted text as a string literal.</summary>
    public static bool DoubleQuotedStrings(this SqlDialect d) => d is SqlDialect.MySql;

    public static bool BackslashEscapes(this SqlDialect d) => d is SqlDialect.MySql;

    public static bool DollarQuotes(this SqlDialect d) => d is SqlDialect.Postgres or SqlDialect.Ansi;

    /// <summary>
    /// Best-effort dialect detection from characteristic syntax. Each regex adds its weight to its dialect; the
    /// highest score wins and ANSI is returned when nothing stands out.
    /// </summary>
    public static SqlDialect Detect(IEnumerable<string> texts)
    {
        var scores = new Dictionary<SqlDialect, int>();
        foreach (var text in texts)
        {
            foreach (var (regex, dialect, weight) in Signals)
            {
                var n = Math.Min(regex.Count(text), 20);
                if (n > 0)
                {
                    scores[dialect] = scores.GetValueOrDefault(dialect) + (n * weight);
                }
            }
        }

        var best = scores.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).FirstOrDefault();
        return best.Value >= 3 ? best.Key : SqlDialect.Ansi;
    }

    private static readonly (Regex Regex, SqlDialect Dialect, int Weight)[] Signals =
    [
        (GoLine(), SqlDialect.TSql, 5),
        (TsqlWords(), SqlDialect.TSql, 2),
        (TsqlVariable(), SqlDialect.TSql, 1),
        (Bracketed(), SqlDialect.TSql, 1),
        (DollarQuote(), SqlDialect.Postgres, 4),
        (PgWords(), SqlDialect.Postgres, 2),
        (PgCast(), SqlDialect.Postgres, 1),
        (MySqlWords(), SqlDialect.MySql, 3),
        (Backtick(), SqlDialect.MySql, 1),
        (OracleWords(), SqlDialect.PlSql, 3),
        (SlashLine(), SqlDialect.PlSql, 2),
        (SqliteWords(), SqlDialect.Sqlite, 4),
    ];

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoLine();

    [GeneratedRegex(@"\b(NVARCHAR|UNIQUEIDENTIFIER|IDENTITY\s*\(|NONCLUSTERED|CLUSTERED|SET\s+NOCOUNT|DATETIME2|sp_executesql|TRY_CONVERT|CREATE\s+OR\s+ALTER)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TsqlWords();

    [GeneratedRegex(@"(?<![\w@])@[A-Za-z_]\w*")]
    private static partial Regex TsqlVariable();

    [GeneratedRegex(@"\[[A-Za-z_][\w ]*\]\.")]
    private static partial Regex Bracketed();

    [GeneratedRegex(@"\$[A-Za-z_]*\$")]
    private static partial Regex DollarQuote();

    [GeneratedRegex(@"\b(plpgsql|SERIAL|BIGSERIAL|JSONB|TIMESTAMPTZ|ILIKE|CREATE\s+EXTENSION|RETURNS\s+TRIGGER|ON\s+CONFLICT|search_path)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PgWords();

    [GeneratedRegex(@"\w::\w")]
    private static partial Regex PgCast();

    [GeneratedRegex(@"\b(AUTO_INCREMENT|ENGINE\s*=|DELIMITER|UNSIGNED|TINYINT|DEFAULT\s+CHARSET|ON\s+DUPLICATE\s+KEY)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MySqlWords();

    [GeneratedRegex(@"`\w+`")]
    private static partial Regex Backtick();

    [GeneratedRegex(@"\b(VARCHAR2|NVARCHAR2|NUMBER\s*\(|PACKAGE\s+BODY|CREATE\s+OR\s+REPLACE\s+PACKAGE|PLS_INTEGER|SYSDATE|DBMS_\w+|NOCOPY|EXCEPTION\s+WHEN|%ROWTYPE|%TYPE)", RegexOptions.IgnoreCase)]
    private static partial Regex OracleWords();

    [GeneratedRegex(@"^\s*/\s*$", RegexOptions.Multiline)]
    private static partial Regex SlashLine();

    [GeneratedRegex(@"\b(AUTOINCREMENT|PRAGMA|WITHOUT\s+ROWID)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SqliteWords();
}
