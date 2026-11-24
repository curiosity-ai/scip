using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Scip.Indexer.DotNet;

using Document = Microsoft.CodeAnalysis.Document;

/// <summary>Orchestrates Roslyn and MSBuild to SCIP-index the given solutions/projects.</summary>
internal sealed class ScipProjectIndexer
{
    /// <summary>Referenced external symbols, keyed by SCIP symbol; copied to <c>Index.external_symbols</c>.</summary>
    public Dictionary<string, SymbolInformation> ExternalSymbols { get; } = [];

    /// <summary>Symbols that have an in-source definition (never emitted as external).</summary>
    public HashSet<string> DefinedSymbols { get; } = [];

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static void Restore(DotNetIndexOptions options, FileInfo project)
    {
        var isSolution = project.Extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                         || project.Extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
        var arguments = isSolution
            ? $"restore \"{project.FullName}\" /p:EnableWindowsTargeting=true"
            : "restore /p:EnableWindowsTargeting=true";
        if (options.NugetConfigPath != null)
        {
            arguments += $" --configfile \"{options.NugetConfigPath.FullName}\"";
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = options.WorkingDirectory.FullName,
                FileName = "dotnet",
                Arguments = arguments,
            },
        };
        Console.Error.WriteLine($"$ dotnet {arguments}");
        process.Start();
        if (!process.WaitForExit(options.DotnetRestoreTimeout))
        {
            Console.Error.WriteLine(
                $"warning: dotnet restore did not finish in {options.DotnetRestoreTimeout} ms, the results of the indexing might be incorrect.");
        }
    }

    public async IAsyncEnumerable<Scip.Document> IndexDocuments(
        MSBuildWorkspace workspace,
        DotNetIndexOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var indexedProjects = new HashSet<string>(PathComparer);
        foreach (var project in options.Projects)
        {
            await foreach (var document in IndexProject(workspace, options, project, indexedProjects, cancellationToken))
            {
                yield return document;
            }
        }
    }

    private async IAsyncEnumerable<Scip.Document> IndexProject(
        MSBuildWorkspace workspace,
        DotNetIndexOptions options,
        FileInfo rootProject,
        HashSet<string> indexedProjects,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (indexedProjects.Contains(rootProject.FullName))
        {
            yield break;
        }

        var isProjectFile = string.Equals(rootProject.Extension, ".csproj", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(rootProject.Extension, ".vbproj", StringComparison.OrdinalIgnoreCase);
        var projects = isProjectFile
            ? workspace.CurrentSolution.Projects
                .Where(project => PathComparer.Equals(project.FilePath, rootProject.FullName)).ToList()
            : [];
        if (projects.Count == 0)
        {
            if (!options.SkipDotnetRestore)
            {
                Restore(options, rootProject);
            }

            if (isProjectFile)
            {
                await workspace.OpenProjectAsync(rootProject.FullName, cancellationToken: cancellationToken);
                // OpenProjectAsync returns only the first target framework; select from all loaded variants.
                projects = workspace.CurrentSolution.Projects
                    .Where(project => PathComparer.Equals(project.FilePath, rootProject.FullName)).ToList();
            }
            else
            {
                projects = (await workspace.OpenSolutionAsync(rootProject.FullName, cancellationToken: cancellationToken))
                    .Projects.ToList();
            }
        }

        var framework = $"net{Environment.Version.Major}.0";
        foreach (var projectGroup in projects.GroupBy(x => x.FilePath!, PathComparer))
        {
            // Index one target framework per project file, preferring the running framework.
            var project = projectGroup.FirstOrDefault(x =>
                              x.Name.Contains($"({framework})", StringComparison.OrdinalIgnoreCase))
                          ?? projectGroup.First();
            if (project.Language != "C#" && project.Language != "Visual Basic")
            {
                Console.Error.WriteLine(
                    $"warning: Skipping project {project.FilePath} because it has language {project.Language} " +
                    "and scip-dotnet currently only supports C# and Visual Basic.");
                continue;
            }

            if (!indexedProjects.Add(project.FilePath!))
            {
                continue;
            }

            var globals = new Dictionary<ISymbol, ScipSymbol>(SymbolEqualityComparer.Default);
            foreach (var document in project.Documents)
            {
                if (options.Matcher.Match(options.WorkingDirectory.FullName, document.FilePath ?? "").HasMatches)
                {
                    yield return await IndexDocument(document, options, globals, project.Language, cancellationToken);
                }
            }
        }
    }

    private async Task<Scip.Document> IndexDocument(
        Document document,
        DotNetIndexOptions options,
        Dictionary<ISymbol, ScipSymbol> globals,
        string language,
        CancellationToken cancellationToken)
    {
        Scip.Document doc = new()
        {
            Language = language,
            PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
            RelativePath = document.FilePath == null
                ? ""
                : Path.GetRelativePath(options.WorkingDirectory.FullName, document.FilePath),
        };
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null)
        {
            Console.Error.WriteLine(
                $"warning: Skipping document {document.FilePath} because GetSemanticModelAsync() returned null");
            return doc;
        }

        var indexer = new ScipDocumentIndexer(doc, options, globals, ExternalSymbols, DefinedSymbols);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (language == "C#")
        {
            new ScipCSharpSyntaxWalker(indexer, semanticModel).Visit(root);
        }
        else
        {
            new ScipVisualBasicSyntaxWalker(indexer, semanticModel).Visit(root);
        }

        return doc;
    }
}
