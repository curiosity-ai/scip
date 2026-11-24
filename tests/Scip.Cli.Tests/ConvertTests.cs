using Microsoft.Data.Sqlite;
using Scip.Cli.Convert;
using ZstdSharp;
using static Scip.Cli.Tests.TestHelpers;

namespace Scip.Cli.Tests;

public class ConvertTests
{
    private static Index TestIndex1()
    {
        const string sym = "scip-go go . . pkg1/S1#";
        var a = new Document { RelativePath = "a.go" };
        a.Occurrences.Add(Occ(sym, new Range(10, 3, 10, 6), SymbolRole.Definition));
        a.Symbols.Add(new SymbolInformation { Symbol = sym });
        var b = new Document { RelativePath = "b.go" };
        b.Occurrences.Add(Occ(sym, new Range(15, 9, 15, 12)));
        return new Index { Documents = { a, b } };
    }

    private static List<T> Query<T>(SqliteConnection db, string sql, Func<SqliteDataReader, T> read)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var result = new List<T>();
        while (reader.Read())
        {
            result.Add(read(reader));
        }

        return result;
    }

    [Test]
    public async Task SmokeTest()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var index = TestIndex1();
            using var db = SqliteConverter.CreateDatabase(Path.Combine(dir.FullName, "index.db"));
            using (var converter = new SqliteConverter(db, SqliteConverter.ChunkSizeHint))
            {
                converter.Convert(index);
            }

            // documents
            var paths = Query(db, "SELECT relative_path FROM documents", static r => r.GetString(0)).Order().ToList();
            await Assert.That(paths).IsEquivalentTo(index.Documents.Select(static d => d.RelativePath).Distinct().Order());

            // symbols
            var symbols = Query(db, "SELECT symbol FROM global_symbols", static r => r.GetString(0)).Order().ToList();
            var expectedSymbols = index.Documents
                .SelectMany(static d => d.Occurrences.Select(static o => o.Symbol).Concat(d.Symbols.Select(static s => s.Symbol)))
                .Distinct().Order().ToList();
            await Assert.That(symbols).IsEquivalentTo(expectedSymbols);

            // occurrences
            using var decompressor = new Decompressor();
            var obtained = Query(
                db,
                "SELECT d.relative_path, occurrences FROM documents d JOIN chunks c ON c.document_id = d.id",
                r => (Path: r.GetString(0), Chunk: Chunk.FromDbFormat((byte[])r.GetValue(1), decompressor)))
                .SelectMany(static x => x.Chunk.Occurrences.Select(o => (x.Path, o.Symbol, Role: o.SymbolRoles, Range: o.GetSourceRange()!.Value)))
                .OrderBy(static x => x.Path, StringComparer.Ordinal).ThenBy(static x => x.Range.Start.Line)
                .ToList();
            var expected = index.Documents
                .SelectMany(static d => d.Occurrences.Select(o => (Path: d.RelativePath, o.Symbol, Role: o.SymbolRoles, Range: o.GetSourceRange()!.Value)))
                .OrderBy(static x => x.Path, StringComparer.Ordinal).ThenBy(static x => x.Range.Start.Line)
                .ToList();
            await Assert.That(obtained).IsEquivalentTo(expected);

            // mentions
            var mentions = Query(db, "SELECT COUNT(*) FROM mentions", static r => r.GetInt64(0));
            await Assert.That(mentions[0]).IsEqualTo(2L);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            dir.Delete(true);
        }
    }

    [Test]
    public async Task EnclosingRangesAndMetadataAreStored()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            const string sym = "scip-go go . . pkg1/F().";
            var doc = new Document { RelativePath = "a.go", Language = "go", PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart };
            var occ = Occ(sym, new Range(1, 5, 1, 6), SymbolRole.Definition);
            occ.SetEnclosingSourceRange(new Range(1, 0, 4, 1));
            doc.Occurrences.Add(occ);
            doc.Symbols.Add(new SymbolInformation
            {
                Symbol = sym,
                DisplayName = "F",
                Kind = SymbolInformation.Types.Kind.Function,
                Documentation = { "a", "b" },
            });

            var dbPath = Path.Combine(dir.FullName, "sub", "index.db");
            var indexPath = WriteIndex(dir, new Index { Documents = { doc } });
            var (code, output, _) = await RunCli("expt-convert", "--output", dbPath, indexPath);
            await Assert.That(code).IsEqualTo(0);
            await Assert.That(output).Contains("Successfully converted SCIP index to SQLite database at");

            using var db = new SqliteConnection($"Data Source={dbPath};Pooling=False");
            db.Open();
            var ranges = Query(db, "SELECT start_line, start_char, end_line, end_char FROM defn_enclosing_ranges",
                static r => (r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3)));
            await Assert.That(ranges).IsEquivalentTo(new[] { (1L, 0L, 4L, 1L) });
            var docs = Query(db, "SELECT language, position_encoding FROM documents", static r => (r.GetString(0), r.GetString(1)));
            await Assert.That(docs).IsEquivalentTo(new[] { ("go", "UTF-16") });
            var info = Query(db, "SELECT display_name, kind, documentation FROM global_symbols", static r => (r.GetString(0), r.GetInt64(1), r.GetString(2)));
            await Assert.That(info).IsEquivalentTo(new[] { ("F", (long)SymbolInformation.Types.Kind.Function, "a\nb") });
            var indexes = Query(db, "SELECT name FROM sqlite_master WHERE type = 'index' AND name LIKE 'idx_%'", static r => r.GetString(0));
            await Assert.That(indexes.Count).IsEqualTo(6);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            dir.Delete(true);
        }
    }

    [Test]
    public async Task ChunksNeverSplitALine()
    {
        var occurrences = new List<Occurrence>();
        for (var i = 0; i < 7; i++)
        {
            occurrences.Add(Occ("local 1", new Range(i / 3, i, i / 3, i + 1)));
        }

        // Lines: 0,0,0,1,1,1,2; chunk size 2 must extend chunks to finish a line.
        var chunks = SqliteConverter.ChunkOccurrences(occurrences, 2);
        await Assert.That(chunks.Select(static c => c.Occurrences.Count)).IsEquivalentTo(new[] { 3, 3, 1 });
        await Assert.That(chunks.Select(static c => (c.StartLine, c.EndLine))).IsEquivalentTo(new[] { (0, 0), (1, 1), (2, 2) });
        await Assert.That(SqliteConverter.ChunkOccurrences([], 2)).IsEmpty();
    }
}
