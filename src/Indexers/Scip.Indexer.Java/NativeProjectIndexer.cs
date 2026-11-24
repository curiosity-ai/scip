using Scip.Indexer.Java.Project;
using Scip.Indexer.Java.Semantics;
using Scip.Indexer.Java.Syntax;

namespace Scip.Indexer.Java;

/// <summary>
/// The native pipeline: parse every source file, resolve symbols against sources, dependency jars and the JDK image
/// (all read natively), and stream the SCIP index. No javac, plugin jar or build tool is involved.
/// </summary>
public static class NativeProjectIndexer
{
    public static async Task<int> RunAsync(JavaProject project, string output, string? jdkVersion, IReadOnlyList<string> arguments, AggregatorReporter reporter, CancellationToken ct)
    {
        using var ws = new Workspace();
        var jdkOk = ws.Classes.UseJdk(project.JavaHome, jdkVersion);
        if (!jdkOk) reporter.Warning("no JDK runtime image found (set JAVA_HOME or --jdk-home): references to JDK classes are not resolved");
        foreach (var dep in project.Dependencies) ws.Classes.AddJar(dep);
        foreach (var note in project.Notes) reporter.Warning(note);

        var files = new Dictionary<string, JavaPackage>(StringComparer.Ordinal);
        foreach (var root in project.SourceRoots)
        {
            if (!Directory.Exists(root.Directory)) continue;
            foreach (var f in ProjectDiscovery.EnumerateJavaFiles(root.Directory)) files.TryAdd(Path.GetFullPath(f), root.Package);
        }
        foreach (var (f, p) in project.SourceFiles) files.TryAdd(Path.GetFullPath(f), p);

        var sources = new List<JavaSourceFile>();
        foreach (var (path, pkg) in files.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            if (Path.GetFileName(path) is "module-info.java" or "package-info.java") continue;
            var rel = Path.GetRelativePath(project.Root, path).Replace('\\', '/');
            try
            {
                sources.Add(ws.AddFile(rel, await File.ReadAllTextAsync(path, ct), pkg));
            }
            catch (ParseException e)
            {
                reporter.Warning($"{rel}: cannot parse ({e.Message} at offset {e.Offset}); file skipped");
            }
        }
        if (sources.Count == 0 && !reporter.HasErrors)
        {
            reporter.ReportError("no Java sources found to index in " + project.Root);
            return 1;
        }

        var tmp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, $".scip-java-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = File.Create(tmp))
            using (var writer = new IndexWriter(stream))
            {
                var tool = new ToolInfo { Name = "scip-java", Version = typeof(NativeProjectIndexer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0" };
                tool.Arguments.AddRange(arguments);
                writer.WriteMetadata(new Metadata
                {
                    Version = ProtocolVersion.UnspecifiedProtocolVersion,
                    ProjectRoot = new Uri(project.Root.TrimEnd('/', '\\') + "/").AbsoluteUri,
                    TextDocumentEncoding = TextEncoding.Utf8,
                    ToolInfo = tool,
                });
                var documents = new List<Document>();
                foreach (var file in sources)
                {
                    try
                    {
                        documents.Add(new DocumentIndexer(ws, file).Index());
                    }
                    catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or ArgumentException or NullReferenceException or InvalidCastException)
                    {
                        reporter.Warning($"{file.RelativePath}: indexing failed ({e.GetType().Name}: {e.Message}); emitting an empty document");
                        documents.Add(new Document { Language = "java", RelativePath = file.RelativePath, PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart });
                    }
                }
                // "find implementations": add inverse relationships (overridden -> overriding) across documents.
                var inverse = ShardAggregator.InverseReferences(documents);
                foreach (var doc in documents)
                {
                    foreach (var info in doc.Symbols)
                    {
                        if (!inverse.TryGetValue(info.Symbol, out var overriders)) continue;
                        foreach (var o in overriders) info.Relationships.Add(new Relationship { Symbol = o, IsImplementation = true, IsReference = true });
                    }
                    writer.WriteDocument(doc);
                }
            }
            File.Move(tmp, output, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
        return 0;
    }
}
