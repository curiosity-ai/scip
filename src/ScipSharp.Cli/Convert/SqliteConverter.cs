#pragma warning disable CS0612 // deprecated range fields are intentionally supported

using Google.Protobuf;
using Microsoft.Data.Sqlite;
using ZstdSharp;

namespace ScipSharp.Cli.Convert;

/// <summary>A run of occurrences starting on a contiguous range of lines.</summary>
public sealed record Chunk(int StartLine, int EndLine, IReadOnlyList<Occurrence> Occurrences)
{
    /// <summary>Serializes the occurrences as a <see cref="Document"/> message and compresses it with zstd.</summary>
    public byte[] ToDbFormat(Compressor compressor)
    {
        var document = new Document();
        document.Occurrences.AddRange(Occurrences);
        return compressor.Wrap(document.ToByteArray()).ToArray();
    }

    /// <summary>Inverse of <see cref="ToDbFormat"/>.</summary>
    public static Chunk FromDbFormat(byte[] blob, Decompressor decompressor)
    {
        var document = Document.Parser.ParseFrom(decompressor.Unwrap(blob).ToArray());
        return new Chunk(0, 0, [.. document.Occurrences]);
    }
}

/// <summary>Converts a SCIP index to a SQLite database (port of the converter in <c>convert.go</c>).</summary>
public sealed class SqliteConverter(SqliteConnection connection, int chunkSize) : IDisposable
{
    /// <summary>Number of occurrences per chunk used by the CLI.</summary>
    public const int ChunkSizeHint = 200;

    private readonly Compressor _compressor = new();

    public void Dispose() => _compressor.Dispose();

    /// <summary>Creates the schema in an empty database.</summary>
    public static void CreateSchema(SqliteConnection conn)
    {
        ExecuteAll(conn,
        [
            "PRAGMA synchronous = normal;",
            "PRAGMA temp_store = memory;",
            "PRAGMA foreign_keys = ON;",
            "PRAGMA journal_mode = WAL;",
            """
            CREATE TABLE documents (
                id INTEGER PRIMARY KEY,
                language TEXT,
                relative_path TEXT NOT NULL UNIQUE,
                position_encoding TEXT,
                text TEXT
            );
            """,
            """
            CREATE TABLE chunks (
                id INTEGER PRIMARY KEY,
                document_id INTEGER NOT NULL,
                chunk_index INTEGER NOT NULL,
                start_line INTEGER NOT NULL,
                end_line INTEGER NOT NULL,
                occurrences BLOB NOT NULL,
                FOREIGN KEY (document_id) REFERENCES documents(id)
            );
            """,
            """
            CREATE TABLE global_symbols (
                id INTEGER PRIMARY KEY,
                symbol TEXT NOT NULL UNIQUE,
                display_name TEXT,
                kind INTEGER,
                documentation TEXT,
                signature BLOB,
                enclosing_symbol TEXT,
                relationships BLOB
            );
            """,
            """
            CREATE TABLE mentions (
                chunk_id INTEGER NOT NULL,
                symbol_id INTEGER NOT NULL,
                role INTEGER NOT NULL,
                PRIMARY KEY (chunk_id, symbol_id, role),
                FOREIGN KEY (chunk_id) REFERENCES chunks(id),
                FOREIGN KEY (symbol_id) REFERENCES global_symbols(id)
            );
            """,
            """
            CREATE TABLE defn_enclosing_ranges (
                id INTEGER PRIMARY KEY,
                document_id INTEGER NOT NULL,
                symbol_id INTEGER NOT NULL,
                start_line INTEGER NOT NULL,
                start_char INTEGER NOT NULL,
                end_line INTEGER NOT NULL,
                end_char INTEGER NOT NULL,
                FOREIGN KEY (document_id) REFERENCES documents(id),
                FOREIGN KEY (symbol_id) REFERENCES global_symbols(id)
            );
            """,
        ]);
    }

    /// <summary>Creates the secondary indexes (done after loading for speed).</summary>
    public static void PrepareIndexes(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        ExecuteAll(conn,
        [
            "CREATE INDEX idx_chunks_line_range ON chunks(document_id, start_line, end_line);",
            "CREATE INDEX idx_mentions_symbol_id_role ON mentions(symbol_id, role);",
            "CREATE INDEX idx_defn_enclosing_ranges_symbol_id ON defn_enclosing_ranges(symbol_id);",
            "CREATE INDEX idx_defn_enclosing_ranges_document ON defn_enclosing_ranges(document_id, start_line, end_line);",
            "CREATE INDEX idx_chunks_doc_id ON chunks(document_id)",
            "CREATE INDEX idx_global_symbols_symbol ON global_symbols(symbol);",
        ]);
        tx.Commit();
    }

    /// <summary>Opens (and truncates) a database file with the schema created.</summary>
    public static SqliteConnection CreateDatabase(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(path, []); // truncates a pre-existing file
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        try
        {
            conn.Open();
            CreateSchema(conn);
        }
        catch
        {
            conn.Dispose();
            throw;
        }

        return conn;
    }

    private static void ExecuteAll(SqliteConnection conn, IEnumerable<string> statements)
    {
        foreach (var statement in statements)
        {
            try
            {
                using var command = conn.CreateCommand();
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }
            catch (SqliteException e)
            {
                throw new InvalidOperationException($"failed to execute statement: {statement}: {e.Message}", e);
            }
        }
    }

    /// <summary>Writes every document of <paramref name="index"/> in a single transaction. Documents are canonicalized in place.</summary>
    public void Convert(Index index)
    {
        using var tx = connection.BeginTransaction();
        var docPositions = new Dictionary<string, (int Index, long Id)>(StringComparer.Ordinal);
        var symbolToId = new Dictionary<string, long>(StringComparer.Ordinal);

        for (var i = 0; i < index.Documents.Count; i++)
        {
            var doc = index.Documents[i];
            if (docPositions.TryGetValue(doc.RelativePath, out var existing))
            {
                Console.Error.WriteLine(
                    $"warning: found multiple documents with identical relative path; ignoring duplicates: path={doc.RelativePath} firstIndex={existing.Index} duplicateIndex={i}");
                continue;
            }

            doc = Canonicalizer.CanonicalizeDocument(doc);
            index.Documents[i] = doc;

            long docId;
            try
            {
                docId = InsertDocument(doc, tx);
            }
            catch (Exception e) when (e is SqliteException or InvalidOperationException)
            {
                throw new InvalidOperationException($"document at index {i}: {e.Message}", e);
            }

            docPositions[doc.RelativePath] = (i, docId);

            foreach (var symbol in doc.Symbols)
            {
                if (symbol.Symbol.Length == 0)
                {
                    throw new InvalidOperationException($"empty symbol in SymbolInformation for document \"{doc.RelativePath}\"");
                }

                if (symbol.Symbol.IsLocalSymbol() || symbolToId.ContainsKey(symbol.Symbol))
                {
                    continue;
                }

                symbolToId[symbol.Symbol] = InsertGlobalSymbol(symbol, tx);
            }
        }

        foreach (var (position, docId) in docPositions.Values)
        {
            var doc = index.Documents[position];
            InsertEnclosingRangeData(symbolToId, doc, docId, tx);
            InsertOccurrenceData(doc, docId, symbolToId, tx);
        }

        // External symbols are intentionally not processed.
        tx.Commit();
    }

    private SqliteCommand Command(SqliteTransaction tx, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    private void InsertOccurrenceData(Document doc, long docId, Dictionary<string, long> symbolToId, SqliteTransaction tx)
    {
        var chunkIndex = 0;
        foreach (var chunk in ChunkOccurrences(doc.Occurrences, chunkSize))
        {
            var chunkId = InsertChunk(chunk, docId, chunkIndex++, tx);

            var symbolRoles = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            foreach (var occ in chunk.Occurrences)
            {
                if (occ.Symbol.IsLocalSymbol())
                {
                    continue;
                }

                if (!symbolRoles.TryGetValue(occ.Symbol, out var roles))
                {
                    symbolRoles[occ.Symbol] = roles = [];
                }

                roles.Add(occ.SymbolRoles);
                if (!symbolToId.ContainsKey(occ.Symbol))
                {
                    symbolToId[occ.Symbol] = InsertGlobalSymbol(new SymbolInformation { Symbol = occ.Symbol }, tx);
                }
            }

            foreach (var (symbol, roles) in symbolRoles)
            {
                foreach (var role in roles)
                {
                    using var mention = Command(
                        tx,
                        "INSERT INTO mentions (chunk_id, symbol_id, role) VALUES ($c, $s, $r)",
                        ("$c", chunkId), ("$s", symbolToId[symbol]), ("$r", (long)role));
                    mention.ExecuteNonQuery();
                }
            }
        }
    }

    private void InsertEnclosingRangeData(Dictionary<string, long> symbolToId, Document doc, long docId, SqliteTransaction tx)
    {
        foreach (var occ in doc.Occurrences)
        {
            // Technically, local symbols could be part of the hierarchy, but ignore them for now.
            if (!SymbolRole.Definition.Matches(occ) || occ.Symbol.IsLocalSymbol()
                || !occ.TryGetEnclosingSourceRange(out var enclosing))
            {
                continue;
            }

            if (enclosing.Validate() is { } rangeError)
            {
                throw new InvalidOperationException(
                    $"in document \"{doc.RelativePath}\": bad enclosing range {enclosing} for symbol \"{occ.Symbol}\": {Range.Describe(rangeError)}");
            }

            if (!symbolToId.TryGetValue(occ.Symbol, out var symbolId))
            {
                throw new InvalidOperationException(
                    $"in document \"{doc.RelativePath}\": symbol \"{occ.Symbol}\" has definition occurrence, but no SymbolInformation");
            }

            using var command = Command(
                tx,
                "INSERT INTO defn_enclosing_ranges (document_id, symbol_id, start_line, start_char, end_line, end_char) VALUES ($d, $s, $sl, $sc, $el, $ec)",
                ("$d", docId), ("$s", symbolId),
                ("$sl", (long)enclosing.Start.Line), ("$sc", (long)enclosing.Start.Character),
                ("$el", (long)enclosing.End.Line), ("$ec", (long)enclosing.End.Character));
            command.ExecuteNonQuery();
        }
    }

    private long InsertGlobalSymbol(SymbolInformation symbol, SqliteTransaction tx)
    {
        var documentation = string.Join('\n', symbol.Documentation);
        using var command = Command(
            tx,
            """
            INSERT INTO global_symbols (symbol, display_name, kind, documentation, enclosing_symbol)
            VALUES ($sym, $name, $kind, $doc, $encl)
            ON CONFLICT(symbol) DO NOTHING
            RETURNING id
            """,
            ("$sym", symbol.Symbol),
            ("$name", symbol.DisplayName.Length == 0 ? null : symbol.DisplayName),
            ("$kind", symbol.Kind == SymbolInformation.Types.Kind.UnspecifiedKind ? null : (long)symbol.Kind),
            ("$doc", documentation.Length == 0 ? null : documentation),
            ("$encl", symbol.EnclosingSymbol.Length == 0 ? null : symbol.EnclosingSymbol));
        return command.ExecuteScalar() is long id
            ? id
            : throw new InvalidOperationException(
                $"expected map to de-duplicate inserts eagerly, but attempted to insert symbol \"{symbol.Symbol}\" despite it existing");
    }

    private long InsertChunk(Chunk chunk, long docId, int chunkIndex, SqliteTransaction tx)
    {
        using var command = Command(
            tx,
            """
            INSERT INTO chunks (document_id, chunk_index, start_line, end_line, occurrences)
            VALUES ($d, $i, $s, $e, $o) RETURNING id
            """,
            ("$d", docId), ("$i", (long)chunkIndex), ("$s", (long)chunk.StartLine), ("$e", (long)chunk.EndLine),
            ("$o", chunk.ToDbFormat(_compressor)));
        return (long)command.ExecuteScalar()!;
    }

    private long InsertDocument(Document doc, SqliteTransaction tx)
    {
        if (doc.RelativePath.Length == 0)
        {
            throw new InvalidOperationException("relative path must not be empty");
        }

        var encoding = doc.PositionEncoding switch
        {
            PositionEncoding.UnspecifiedPositionEncoding => null,
            PositionEncoding.Utf8CodeUnitOffsetFromLineStart => "UTF-8",
            PositionEncoding.Utf16CodeUnitOffsetFromLineStart => "UTF-16",
            PositionEncoding.Utf32CodeUnitOffsetFromLineStart => "UTF-32",
            _ => throw new InvalidOperationException($"unknown position encoding {(int)doc.PositionEncoding}"),
        };
        using var command = Command(
            tx,
            """
            INSERT INTO documents (language, relative_path, position_encoding, text)
            VALUES ($lang, $path, $enc, $text)
            RETURNING id
            """,
            ("$lang", doc.Language.Length == 0 ? null : doc.Language),
            ("$path", doc.RelativePath),
            ("$enc", encoding),
            ("$text", doc.Text.Length == 0 ? null : doc.Text));
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>Splits occurrences into chunks of about <paramref name="chunkSize"/>, never splitting a line across chunks.</summary>
    public static List<Chunk> ChunkOccurrences(IReadOnlyList<Occurrence> occurrences, int chunkSize)
    {
        var chunks = new List<Chunk>();
        if (occurrences.Count == 0)
        {
            return chunks;
        }

        int StartLine(int i) => occurrences[i].TryGetSourceRange(out var r) ? r.Start.Line : 0;

        var hi = Math.Min(occurrences.Count, chunkSize);
        for (var lo = 0; lo < hi && hi <= occurrences.Count;)
        {
            while (hi <= occurrences.Count - 1 && StartLine(hi - 1) == StartLine(hi))
            {
                hi++;
            }

            chunks.Add(new Chunk(StartLine(lo), StartLine(hi - 1), [.. occurrences.Skip(lo).Take(hi - lo)]));
            lo = hi;
            hi = Math.Min(occurrences.Count, hi + chunkSize);
        }

        return chunks;
    }
}
