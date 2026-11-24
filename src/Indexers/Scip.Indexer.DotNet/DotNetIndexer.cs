using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Scip.Indexing;

namespace Scip.Indexer.DotNet;

/// <summary>
/// Port of <c>scip-dotnet</c>: indexes C# and Visual Basic solutions/projects using Roslyn and MSBuild.
/// Arguments (after <c>index dotnet</c>): optional <c>.sln/.slnx/.csproj/.vbproj</c> paths, <c>--include</c>,
/// <c>--exclude</c>, <c>--allow-global-symbol-definitions</c>, <c>--dotnet-restore-timeout</c>,
/// <c>--skip-dotnet-restore</c>, <c>--nuget-config-path</c>, and <c>--working-directory</c>/<c>--output</c> overrides.
/// </summary>
public sealed class DotNetIndexer : IIndexer
{
    public string Name => "dotnet";

    public string Languages => "C#, Visual Basic";

    public string UpstreamProject => "scip-dotnet";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        var parsed = DotNetIndexOptions.Parse(options.ProjectRoot, options.OutputPath, options.Arguments);
        if (parsed is null)
        {
            return 1;
        }

        var projects = parsed.Projects.Count > 0 ? parsed.Projects : FindSolutionOrProjectFiles(parsed.WorkingDirectory);
        if (projects.Count == 0)
        {
            return 1;
        }

        parsed = parsed with { Projects = projects };
        EnsureMSBuildRegistered();
        return await RunAsync(parsed, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnsureMSBuildRegistered()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<int> RunAsync(DotNetIndexOptions options, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var indexer = new ScipProjectIndexer();

        var outputDirectory = options.Output.Directory;
        if (outputDirectory is { Exists: false })
        {
            outputDirectory.Create();
        }

        var documentCount = 0;
        List<Scip.Document> documents = [];
        HashSet<string> defined = new(StringComparer.Ordinal), referenced = new(StringComparer.Ordinal);
        await using (var stream = File.Create(options.Output.FullName))
        using (var writer = new IndexWriter(stream))
        {
            writer.WriteMetadata(new Metadata
            {
                ProjectRoot = new Uri(new Uri("file://"), options.WorkingDirectory.FullName).ToString(),
                ToolInfo = new ToolInfo { Name = "scip-dotnet", Version = "0.1.0-SNAPSHOT" },
                TextDocumentEncoding = TextEncoding.Utf8,
            });
            await foreach (var document in indexer.IndexDocuments(options, cancellationToken))
            {
                documents.Add(document);
            }

            // The same file may be compiled by several projects / target frameworks; emit it once.
            documents = DocumentMerger.MergeByPath(documents);
            foreach (var document in documents)
            {
                SymbolCompleteness.CompleteDocument(document, defined, referenced);
            }

            documentCount = documents.Count;
            foreach (var document in documents)
            {
                writer.WriteDocument(document);
            }

            // Dependency hover documentation, minus symbols that also have an in-source definition.
            foreach (var external in indexer.ExternalSymbols
                         .Where(entry => !indexer.DefinedSymbols.Contains(entry.Key))
                         .OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                writer.WriteExternalSymbol(external.Value);
                defined.Add(external.Key);
            }

            // `scip lint` requires a SymbolInformation for every referenced symbol (e.g. namespaces, which
            // Roslyn gives no hover text for); emit bare external entries for the rest.
            foreach (var missing in referenced.Where(symbol => !defined.Contains(symbol)).Order(StringComparer.Ordinal))
            {
                writer.WriteExternalSymbol(SymbolCompleteness.StubFor(missing));
            }
        }

        foreach (var project in indexer.FailedProjects.Order(StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"error: failed to load project '{project}'; its sources were not indexed.");
        }

        foreach (var group in indexer.OtherFailures.GroupBy(message => message))
        {
            Console.Error.WriteLine($"error: {group.Key} (occurred {group.Count()} times)");
        }

        if (documentCount == 0)
        {
            Console.Error.WriteLine("error: no documents were indexed. Check that the project builds (`dotnet build`) and restored successfully.");
            return 1;
        }

        Console.Error.WriteLine($"done: {options.Output} ({documentCount} documents) {stopwatch.Elapsed}");
        return indexer.FailedProjects.Count > 0 ? 1 : 0;
    }

    private static List<FileInfo> FindSolutionOrProjectFiles(DirectoryInfo workingDirectory)
    {
        string[] extensions = [".sln", ".slnx", ".csproj", ".vbproj"];
        var paths = Directory.GetFiles(workingDirectory.FullName)
            .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .ToList();
        if (paths.Count == 0)
        {
            Console.Error.WriteLine(
                $"error: No solution (.sln/.slnx) or .csproj/.vbproj file detected in the working directory '{workingDirectory.FullName}'. " +
                "To fix this problem, pass the path of a solution (.sln/.slnx) or project (.csproj/.vbproj) file to the " +
                "`scip-dotnet index` command. For example, run: scip-dotnet index SOLUTION_FILE");
        }

        return paths;
    }
}
