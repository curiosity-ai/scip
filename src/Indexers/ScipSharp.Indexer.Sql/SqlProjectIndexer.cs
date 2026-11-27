using System.Text;
using ScipSharp.Indexer.Sql.Semantics;
using ScipSharp.Indexer.Sql.Syntax;

namespace ScipSharp.Indexer.Sql;

/// <summary>Options of the SQL indexer.</summary>
public sealed record SqlIndexerOptions
{
    /// <summary>Forces a dialect for every file; null means auto-detect per file (falling back to the project-wide guess).</summary>
    public SqlDialect? Dialect { get; init; }

    /// <summary>Schema of unqualified names (default: the dialect's, e.g. <c>public</c>, <c>dbo</c>).</summary>
    public string? DefaultSchema { get; init; }

    public string? PackageName { get; init; }

    public string PackageVersion { get; init; } = "";

    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Directory names that are never descended into.</summary>
    public IReadOnlySet<string> ExcludedDirectories { get; init; } = DefaultExcludes;

    public static readonly IReadOnlySet<string> DefaultExcludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".hg", ".svn", "node_modules", "target", "dbt_packages", "dbt_modules", "bin", "obj", "artifacts", ".venv", "venv", "__pycache__",
    };
}

/// <summary>
/// Indexes every SQL file below a directory as one project-wide catalog. Files are processed in natural sort order
/// (so that <c>V2__x.sql</c> precedes <c>V10__y.sql</c> in migration directories), in two passes: a catalog pass that
/// replays all DDL, then an emitting pass that resolves references (falling back to the final catalog for forward
/// references) and writes one SCIP document per file.
/// </summary>
public sealed class SqlProjectIndexer(string root, SqlIndexerOptions options, Action<string>? warn = null)
{
    public const string ToolName = "scip-sql";

    public const string ToolVersion = "0.1.0";

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sql", ".ddl", ".dml", ".pks", ".pkb", ".pls", ".plb", ".pck", ".prc", ".fnc", ".trg", ".tps", ".tpb", ".pgsql", ".psql", ".plpgsql", ".mysql", ".tsql", ".sqlite",
    };

    /// <summary>Discovers the SQL files of the project, relative to the root, in natural order.</summary>
    public List<string> DiscoverFiles()
    {
        var result = new List<string>();
        if (File.Exists(root))
        {
            return [Path.GetFileName(root)];
        }

        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(dir).ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                warn?.Invoke($"warning: cannot read {dir}: {e.Message}");
                continue;
            }

            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    if (!options.ExcludedDirectories.Contains(name) && !name.StartsWith('.'))
                    {
                        stack.Push(entry);
                    }
                }
                else if (Extensions.Contains(Path.GetExtension(entry)))
                {
                    result.Add(Path.GetRelativePath(root, entry).Replace(Path.DirectorySeparatorChar, '/'));
                }
            }
        }

        result.Sort(NaturalComparer.Instance);
        return result;
    }

    private string BaseDirectory => File.Exists(root) ? Path.GetDirectoryName(Path.GetFullPath(root))! : root;

    public Metadata CreateMetadata()
    {
        var full = Path.GetFullPath(BaseDirectory).Replace(Path.DirectorySeparatorChar, '/');
        var meta = new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ProjectRoot = "file://" + (full.StartsWith('/') ? "" : "/") + full,
            TextDocumentEncoding = TextEncoding.Utf8,
            ToolInfo = new ToolInfo { Name = ToolName, Version = ToolVersion },
        };
        meta.ToolInfo.Arguments.AddRange(options.Arguments);
        return meta;
    }

    /// <summary>Runs the indexer, streaming documents; returns the external symbols.</summary>
    public List<SymbolInformation> Run(Action<Document> onDocument, Action<Metadata>? onMetadata = null)
    {
        onMetadata?.Invoke(CreateMetadata());
        var files = DiscoverFiles();
        var texts = new List<(string Path, string Text)>();
        foreach (var rel in files)
        {
            try
            {
                var bytes = File.ReadAllBytes(Path.Combine(BaseDirectory, rel));
                texts.Add((rel, DecodeText(bytes)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                warn?.Invoke($"warning: cannot read {rel}: {e.Message}");
            }
        }

        var projectDialect = options.Dialect ?? Dialects.Detect(texts.Select(t => t.Text));
        var parsed = new List<(string Path, SqlDialect Dialect, ParsedScript Script)>();
        foreach (var (path, text) in texts)
        {
            var dialect = options.Dialect ?? FileDialect(text, projectDialect);
            ParsedScript script;
            try
            {
                script = SqlParser.ParseScript(text, dialect);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException)
            {
                warn?.Invoke($"warning: failed to parse {path}: {e.Message}");
                script = SqlParser.ParseScript("", dialect);
            }

            parsed.Add((path, dialect, script));
        }

        var isDbt = File.Exists(Path.Combine(BaseDirectory, "dbt_project.yml"));
        string? ModelOf(string path) =>
            isDbt && path.Split('/').Contains("models") ? Path.GetFileNameWithoutExtension(path) : null;
        var names = new SymbolNames(options.PackageName ?? PackageNameOf(BaseDirectory), options.PackageVersion);
        var state = new ProjectState();
        var catalogPass = new Binder(names, state, options.DefaultSchema, catalogPass: null);
        foreach (var (path, dialect, script) in parsed)
        {
            catalogPass.BindFile(script, dialect, doc: null, ModelOf(path));
        }

        var emitPass = new Binder(names, state, options.DefaultSchema, catalogPass);
        foreach (var (path, dialect, script) in parsed)
        {
            var doc = new DocumentBuilder(path, script.Source);
            emitPass.BindFile(script, dialect, doc, ModelOf(path));
            onDocument(doc.Build());
        }

        var external = new List<SymbolInformation>();
        foreach (var (symbol, (kind, display)) in state.Referenced.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (state.Defined.Contains(symbol))
            {
                continue;
            }

            var info = new SymbolInformation { Symbol = symbol, Kind = kind, DisplayName = display.Length > 0 ? display : DisplayOf(symbol) };
            info.Documentation.Add("Unresolved: not defined in the indexed SQL files.");
            external.Add(info);
        }

        return external;
    }

    /// <summary>Builds the whole index in memory (tests, small projects).</summary>
    public Index Build()
    {
        var index = new Index();
        var ext = Run(index.Documents.Add, m => index.Metadata = m);
        index.ExternalSymbols.AddRange(ext);
        return index;
    }

    private static SqlDialect FileDialect(string text, SqlDialect projectDialect)
    {
        var d = Dialects.Detect([text]);
        return d == SqlDialect.Ansi ? projectDialect : d;
    }

    private static string DecodeText(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static string PackageNameOf(string dir)
    {
        var name = Path.GetFileName(Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar));
        return name.Length == 0 ? "sql" : name;
    }

    private static string DisplayOf(string symbol)
    {
        if (!SymbolParser.TryParse(symbol, out var parsed, out _) || parsed.Descriptors.Count == 0)
        {
            return symbol;
        }

        return parsed.Descriptors[^1].Name;
    }
}

/// <summary>Orders strings so that embedded numbers compare numerically (<c>V2</c> &lt; <c>V10</c>), case-insensitively.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (x is null || y is null)
        {
            return string.CompareOrdinal(x, y);
        }

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                var si = i;
                var sj = j;
                while (i < x.Length && char.IsAsciiDigit(x[i]))
                {
                    i++;
                }

                while (j < y.Length && char.IsAsciiDigit(y[j]))
                {
                    j++;
                }

                var a = x.AsSpan(si, i - si).TrimStart('0');
                var b = y.AsSpan(sj, j - sj).TrimStart('0');
                if (a.Length != b.Length)
                {
                    return a.Length.CompareTo(b.Length);
                }

                var c = a.SequenceCompareTo(b);
                if (c != 0)
                {
                    return c;
                }

                continue;
            }

            var cx = char.ToLowerInvariant(x[i]);
            var cy = char.ToLowerInvariant(y[j]);
            if (cx != cy)
            {
                // Directory separators sort before other characters so that a directory's files stay together.
                if (cx == '/')
                {
                    return -1;
                }

                if (cy == '/')
                {
                    return 1;
                }

                return cx.CompareTo(cy);
            }

            i++;
            j++;
        }

        var rest = (x.Length - i).CompareTo(y.Length - j);
        return rest != 0 ? rest : string.CompareOrdinal(x, y);
    }
}
