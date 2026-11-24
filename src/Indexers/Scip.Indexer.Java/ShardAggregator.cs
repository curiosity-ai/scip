using System.IO.Compression;
using Google.Protobuf;

namespace Scip.Indexer.Java;

/// <summary>Callbacks of the aggregator. It never fails fast; errors are collected and surfaced via the exit code.</summary>
public class AggregatorReporter
{
    private int _errors;

    public bool HasErrors => _errors > 0;

    public virtual void ReportError(string message)
    {
        Interlocked.Increment(ref _errors);
        Console.Error.WriteLine("error: " + message);
    }

    public virtual void Warning(string message) => Console.Error.WriteLine("warning: " + message);

    public virtual void Info(string message) => Console.WriteLine(message);
}

/// <summary>Configuration of <see cref="ShardAggregator"/> (port of <c>ScipAggregatorOptions</c>).</summary>
public sealed record AggregatorOptions(
    IReadOnlyList<string> TargetRoots,
    string Output,
    string SourceRoot,
    ToolInfo ToolInfo,
    IReadOnlyList<MavenPackage> Packages,
    JdkPackage Jdk,
    AggregatorReporter Reporter,
    bool EmitInverseRelationships = true,
    bool AllowEmptyIndex = false,
    bool AllowExportingGlobalSymbolsFromDirectoryEntries = false);

/// <summary>
/// Merges the per-source SCIP shards (<c>META-INF/scip/**.scip</c>, one <see cref="Index"/> per source file, produced
/// by the scip-java javac plugin) into one index. Port of <c>ScipAggregator.java</c>: shard symbols are bare
/// descriptors, which are rewritten to full SCIP symbols using a <see cref="PackageTable"/>.
/// </summary>
public sealed class ShardAggregator(AggregatorOptions options)
{
    public static void Run(AggregatorOptions options) => new ShardAggregator(options).Run();

    private AggregatorReporter Reporter => options.Reporter;

    private void Run()
    {
        var packages = new PackageTable(options.Packages, options.Jdk, options.AllowExportingGlobalSymbolsFromDirectoryEntries);
        var rewriter = new SymbolRewriter(packages);
        var shards = FindShards();
        shards.Sort(StringComparer.Ordinal);
        if (Reporter.HasErrors) return;
        if (shards.Count == 0 && !options.AllowEmptyIndex)
        {
            Reporter.ReportError(
                "No SCIP shards found. This typically means that the scip-java compiler plugin was not attached to the " +
                "compilation. Make sure the plugin jar is available and the build tool passes -Xplugin:scip.");
            return;
        }

        var inverse = ComputeInverseReferences(shards);
        var tmp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.Output))!, $".scip-aggregator-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = File.Create(tmp))
            using (var writer = new IndexWriter(stream))
            {
                writer.WriteMetadata(MetadataFor());
                // Rewriting is parallelisable but emission order must stay deterministic: shards are sorted and
                // processed sequentially, matching upstream's output.
                foreach (var shard in shards)
                {
                    foreach (var doc in ReadShard(shard)) writer.WriteDocument(RewriteDocument(doc, rewriter, inverse));
                }
            }
            File.Move(tmp, options.Output, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    private Metadata MetadataFor() => new()
    {
        Version = ProtocolVersion.UnspecifiedProtocolVersion,
        ProjectRoot = new Uri(Path.GetFullPath(options.SourceRoot).TrimEnd('/') + "/").AbsoluteUri,
        TextDocumentEncoding = TextEncoding.Utf8,
        ToolInfo = options.ToolInfo,
    };

    private List<string> FindShards()
    {
        var shards = new List<string>();
        foreach (var root in options.TargetRoots)
        {
            if (root.EndsWith(".jar", StringComparison.Ordinal)) shards.Add(root);
            else if (Directory.Exists(root)) shards.AddRange(Directory.EnumerateFiles(root, "*.scip", SearchOption.AllDirectories));
            else Reporter.Warning("ignoring target root that does not exist or is not a directory: " + root);
        }
        return shards;
    }

    private List<Document> ReadShard(string path)
    {
        var result = new List<Document>();
        try
        {
            if (path.EndsWith(".jar", StringComparison.Ordinal))
            {
                using var zip = ZipFile.OpenRead(path);
                foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".scip", StringComparison.Ordinal)))
                {
                    using var s = entry.Open();
                    result.AddRange(Index.Parser.ParseFrom(s).Documents);
                }
            }
            else
            {
                result.AddRange(Index.Parser.ParseFrom(File.ReadAllBytes(path)).Documents);
            }
        }
        catch (Exception e) when (e is IOException or InvalidProtocolBufferException or InvalidDataException)
        {
            Reporter.ReportError($"invalid SCIP shard: {path} ({e.Message})");
        }
        return result;
    }

    internal static Document RewriteDocument(Document shard, SymbolRewriter rewriter, IReadOnlyDictionary<string, List<string>> inverse)
    {
        var @out = new Document { Language = shard.Language, RelativePath = shard.RelativePath };
        if (shard.Text.Length > 0) @out.Text = shard.Text;

        foreach (var occ in shard.Occurrences)
        {
            var rebuilt = new Occurrence { Symbol = rewriter.Rewrite(occ.Symbol), SymbolRoles = occ.SymbolRoles };
            switch (occ.TypedRangeCase)
            {
                case Occurrence.TypedRangeOneofCase.SingleLineRange: rebuilt.SingleLineRange = occ.SingleLineRange; break;
                case Occurrence.TypedRangeOneofCase.MultiLineRange: rebuilt.MultiLineRange = occ.MultiLineRange; break;
                default: throw new InvalidDataException("expected SCIP 0.9 typed occurrence range");
            }
            switch (occ.TypedEnclosingRangeCase)
            {
                case Occurrence.TypedEnclosingRangeOneofCase.SingleLineEnclosingRange:
                    rebuilt.SingleLineEnclosingRange = occ.SingleLineEnclosingRange; break;
                case Occurrence.TypedEnclosingRangeOneofCase.MultiLineEnclosingRange:
                    rebuilt.MultiLineEnclosingRange = occ.MultiLineEnclosingRange; break;
            }
            @out.Occurrences.Add(rebuilt);
        }

        foreach (var info in shard.Symbols)
        {
            var rebuilt = new SymbolInformation
            {
                Symbol = rewriter.Rewrite(info.Symbol),
                DisplayName = info.DisplayName,
                Kind = info.Kind,
            };
            if (info.SignatureDocumentation is not null) rebuilt.SignatureDocumentation = info.SignatureDocumentation;
            if (info.EnclosingSymbol.Length > 0) rebuilt.EnclosingSymbol = rewriter.Rewrite(info.EnclosingSymbol);
            rebuilt.Documentation.AddRange(info.Documentation);
            foreach (var rel in info.Relationships)
            {
                var copy = rel.Clone();
                copy.Symbol = rewriter.Rewrite(rel.Symbol);
                rebuilt.Relationships.Add(copy);
            }
            if (inverse.TryGetValue(info.Symbol, out var overriders))
            {
                foreach (var overrider in overriders)
                {
                    rebuilt.Relationships.Add(new Relationship
                    {
                        Symbol = rewriter.Rewrite(overrider),
                        IsImplementation = true,
                        IsReference = true,
                    });
                }
            }
            @out.Symbols.Add(rebuilt);
        }
        return @out;
    }

    private Dictionary<string, List<string>> ComputeInverseReferences(List<string> shards)
    {
        if (!options.EmitInverseRelationships) return new Dictionary<string, List<string>>(StringComparer.Ordinal);
        return InverseReferences(shards.SelectMany(ReadShard));
    }

    /// <summary>
    /// Builds <c>overridden-symbol -&gt; [overriding-symbols]</c> for every method-style implementation relationship.
    /// Class-like symbols are excluded (they would pollute find-references), as is <c>java/lang/Object#</c>.
    /// </summary>
    public static Dictionary<string, List<string>> InverseReferences(IEnumerable<Document> documents)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var doc in documents)
        {
            foreach (var info in doc.Symbols)
            {
                if (!SupportsReferenceRelationship(info)) continue;
                if (info.Symbol.Length == 0 || JavaSymbols.IsLocal(info.Symbol)) continue;
                foreach (var rel in info.Relationships)
                {
                    if (!rel.IsImplementation || JavaSymbols.IsLocal(rel.Symbol)) continue;
                    if (rel.Symbol.EndsWith("java/lang/Object#", StringComparison.Ordinal)) continue;
                    if (!result.TryGetValue(rel.Symbol, out var list)) result[rel.Symbol] = list = [];
                    list.Add(info.Symbol);
                }
            }
        }
        return result;
    }

    private static bool SupportsReferenceRelationship(SymbolInformation info) => info.Kind is not (
        SymbolInformation.Types.Kind.Class or SymbolInformation.Types.Kind.Enum or SymbolInformation.Types.Kind.Interface
        or SymbolInformation.Types.Kind.Type or SymbolInformation.Types.Kind.Object
        or SymbolInformation.Types.Kind.PackageObject);
}
