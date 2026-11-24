using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Scip.Indexer.DotNet;

using Document = Microsoft.CodeAnalysis.Document;

/// <summary>Orchestrates Roslyn and MSBuild to SCIP-index the given solutions/projects.</summary>
internal sealed partial class ScipProjectIndexer
{
    /// <summary>Referenced external symbols, keyed by SCIP symbol; copied to <c>Index.external_symbols</c>.</summary>
    public Dictionary<string, SymbolInformation> ExternalSymbols { get; } = [];

    /// <summary>Symbols that have an in-source definition (never emitted as external).</summary>
    public HashSet<string> DefinedSymbols { get; } = [];

    private readonly Dictionary<(string Symbol, string Related), Scip.Document> _emittedRelationships = [];

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static void Restore(DotNetIndexOptions options, FileInfo project)
    {
        var arguments = $"restore \"{project.FullName}\" /p:EnableWindowsTargeting=true";
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

    /// <summary>Project files MSBuild could not load (even after the per-target-framework retry).</summary>
    public HashSet<string> FailedProjects { get; } = new(PathComparer);

    /// <summary>Workspace failure messages that could not be attributed to a project file.</summary>
    public List<string> OtherFailures { get; } = [];

    public async IAsyncEnumerable<Scip.Document> IndexDocuments(
        DotNetIndexOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var indexedProjects = new HashSet<string>(PathComparer);
        var workspaces = new List<MSBuildWorkspace>();
        try
        {
            var primary = MSBuildWorkspace.Create();
            workspaces.Add(primary);
            var seenDiagnostics = 0;
            var retry = new List<string>();
            foreach (var project in options.Projects)
            {
                await foreach (var document in IndexProject(primary, options, project, indexedProjects, cancellationToken))
                {
                    yield return document;
                }

                // Failures of the default load are retried once, per project, with an explicit TargetFramework.
                foreach (var diagnostic in primary.Diagnostics.Skip(seenDiagnostics)
                             .Where(d => d.Kind == WorkspaceDiagnosticKind.Failure))
                {
                    var match = FailedFileRegex().Match(diagnostic.Message);
                    if (match.Success)
                    {
                        var path = match.Groups[1].Value;
                        if (!indexedProjects.Contains(path) && !retry.Contains(path, PathComparer))
                        {
                            retry.Add(path);
                        }
                    }
                    else
                    {
                        OtherFailures.Add(diagnostic.Message);
                    }
                }

                seenDiagnostics = primary.Diagnostics.Count;
            }

            foreach (var path in retry)
            {
                if (indexedProjects.Contains(path))
                {
                    continue;
                }

                var framework = await ResolveTargetFrameworkAsync(path, cancellationToken);
                if (framework == null)
                {
                    FailedProjects.Add(path);
                    continue;
                }

                Console.Error.WriteLine($"info: retrying {path} with TargetFramework={framework}");
                var workspace = MSBuildWorkspace.Create(new Dictionary<string, string> { ["TargetFramework"] = framework });
                workspaces.Add(workspace);
                await foreach (var document in IndexProject(workspace, options, new FileInfo(path), indexedProjects, cancellationToken))
                {
                    yield return document;
                }

                var failures = workspace.Diagnostics
                    .Where(d => d.Kind == WorkspaceDiagnosticKind.Failure && FailedFileRegex().IsMatch(d.Message)).ToList();
                {
                    foreach (var diagnostic in failures)
                    {
                        FailedProjects.Add(FailedFileRegex().Match(diagnostic.Message).Groups[1].Value);
                        Console.Error.WriteLine($"error: {diagnostic.Message}");
                    }
                }
            }
        }
        finally
        {
            foreach (var workspace in workspaces)
            {
                workspace.Dispose();
            }
        }
    }

    [GeneratedRegex(@"processing the file '(.+?)' with message")]
    private static partial Regex FailedFileRegex();

    /// <summary>
    /// Asks MSBuild which target frameworks a project declares and picks one: the running framework if present,
    /// else the highest <c>netX.Y</c>, else the first. Empty entries (e.g. <c>;net10.0</c> from a
    /// <c>$(TargetFrameworks);...</c> append) are skipped, which Roslyn's own splitting does not do.
    /// </summary>
    private static async Task<string?> ResolveTargetFrameworkAsync(string projectPath, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { "msbuild", "-nologo", "-getProperty:TargetFramework", "-getProperty:TargetFrameworks", projectPath },
                WorkingDirectory = Path.GetDirectoryName(projectPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await errors;
            using var json = System.Text.Json.JsonDocument.Parse(output);
            var properties = json.RootElement.GetProperty("Properties");
            var single = properties.GetProperty("TargetFramework").GetString();
            if (!string.IsNullOrWhiteSpace(single))
            {
                return single;
            }

            var all = (properties.GetProperty("TargetFrameworks").GetString() ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var running = $"net{Environment.Version.Major}.0";
            return all.FirstOrDefault(x => x.Equals(running, StringComparison.OrdinalIgnoreCase))
                   ?? all.Where(x => ModernTfmRegex().IsMatch(x))
                       .OrderByDescending(x => Version.Parse(x[3..]))
                       .FirstOrDefault()
                   ?? all.FirstOrDefault();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or System.Text.Json.JsonException
                                      or KeyNotFoundException or InvalidOperationException)
        {
            Console.Error.WriteLine($"warning: could not determine the target frameworks of {projectPath}: {e.Message}");
            return null;
        }
    }

    [GeneratedRegex(@"^net\d+\.\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex ModernTfmRegex();

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

            // A project that failed to load can still show up as an empty stub; it is not "indexed".
            if (!project.Documents.Any() || !indexedProjects.Add(project.FilePath!))
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

        var indexer = new ScipDocumentIndexer(doc, options, globals, ExternalSymbols, DefinedSymbols, _emittedRelationships);
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
