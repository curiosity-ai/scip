using System.Text.RegularExpressions;
using Scip.Indexer.Python.Emit;
using Scip.Indexer.Python.Project;
using Scip.Indexer.Python.Semantics;
using Scip.Indexing;

namespace Scip.Indexer.Python;

/// <summary>Command line options of <c>scip-python index</c> (see <c>MainCommand.ts</c>), plus <c>--python-version</c>.</summary>
public sealed record PythonIndexOptions
{
    public string ProjectName { get; init; } = "";

    public string? ProjectVersion { get; init; }

    public string? ProjectNamespace { get; init; }

    public string? TargetOnly { get; init; }

    public string? EnvironmentFile { get; init; }

    public string? PythonVersion { get; init; }

    public bool InferVersionFromCommit { get; init; } = true;

    public bool Quiet { get; init; }

    public static PythonIndexOptions Parse(IReadOnlyList<string> args, out string? cwd)
    {
        var o = new PythonIndexOptions();
        cwd = null;
        for (var i = 0; i < args.Count; i++)
        {
            string Value() => i + 1 < args.Count ? args[++i] : throw new ArgumentException($"missing value for {args[i]}");
            var arg = args[i];
            var eq = arg.IndexOf('=');
            if (arg.StartsWith("--", StringComparison.Ordinal) && eq > 0)
            {
                args = [.. args.Take(i), arg[..eq], arg[(eq + 1)..], .. args.Skip(i + 1)];
                arg = args[i];
            }

            switch (arg)
            {
                case "--project-name": o = o with { ProjectName = Value() }; break;
                case "--project-version": o = o with { ProjectVersion = Value() }; break;
                case "--project-namespace": o = o with { ProjectNamespace = Value() }; break;
                case "--target-only": o = o with { TargetOnly = Value() }; break;
                case "--environment": o = o with { EnvironmentFile = Value() }; break;
                case "--python-version": o = o with { PythonVersion = Value() }; break;
                case "--cwd": cwd = Value(); break;
                case "--quiet": o = o with { Quiet = true }; break;
                case "--dev": break;
                case "--show-progress-rate-limit": _ = Value(); break;
                default: throw new ArgumentException($"unknown option {arg}");
            }
        }

        return o;
    }
}

/// <summary>
/// Native C# port of <c>scip-python</c>. It contains its own Python parser, binder, import resolver and a small type
/// evaluator, and reads package metadata straight from site-packages. See README.md for what is and is not covered.
/// </summary>
public sealed class PythonIndexer : IIndexer
{
    public string Name => "python";

    public string Languages => "Python";

    public string UpstreamProject => "scip-python";

    public Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        PythonIndexOptions parsed;
        string? cwd;
        try
        {
            parsed = PythonIndexOptions.Parse(options.Arguments, out cwd);
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            return Task.FromResult(2);
        }

        var root = Path.GetFullPath(cwd ?? options.ProjectRoot);
        var output = Path.IsPathRooted(options.OutputPath) ? options.OutputPath : Path.Combine(root, options.OutputPath);
        return Task.FromResult(Run(root, output, parsed, cancellationToken));
    }

    /// <summary>Indexes <paramref name="root"/> and writes the SCIP index to <paramref name="outputPath"/>.</summary>
    public static int Run(string root, string outputPath, PythonIndexOptions options, CancellationToken cancellationToken = default)
    {
        root = Path.GetFullPath(root);
        var config = ProjectConfig.Load(root);

        var name = options.ProjectName;
        var version = options.ProjectVersion;
        if (string.IsNullOrEmpty(name) && config.Name is not null)
        {
            name = config.Name;
        }

        if (string.IsNullOrEmpty(version) && config.Version is not null)
        {
            version = config.Version;
        }

        if (version is null && options.InferVersionFromCommit)
        {
            version = ProjectConfig.GitHead(root);
        }

        if (version is null)
        {
            Console.Error.WriteLine("Could not determine the project version. Pass one explicitly with --project-version.");
            return 1;
        }

        var target = options.TargetOnly is null ? null : Path.GetFullPath(options.TargetOnly);
        var indexRoot = target ?? root;

        var (siteDirs, detectedVersion) = EnvironmentDiscovery.Discover(root);
        var pythonVersion = options.PythonVersion ?? detectedVersion ?? "3.13";
        var environment = options.EnvironmentFile is not null
            ? PythonEnvironment.FromJsonFile(Path.GetFullPath(options.EnvironmentFile))
            : PythonEnvironment.FromSitePackages(siteDirs);

        var modules = new ModuleSystem(root, config.ResolveExtraPaths(), siteDirs, environment);
        var evaluator = new Evaluator(modules);
        var namespaces = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(options.ProjectNamespace))
        {
            namespaces[name] = options.ProjectNamespace;
        }

        var state = new IndexState(
            modules,
            evaluator,
            new SymbolNames(namespaces),
            new PythonPackage(name, version, []),
            new PythonPackage("python-stdlib", pythonVersion, []));

        // File discovery: configured include/exclude, then project files reached through imports.
        var (roots, isExcluded) = config.FileSelection(root);
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in roots)
        {
            if (File.Exists(r))
            {
                if (seen.Add(r))
                {
                    files.Add(r);
                }
            }
            else if (Directory.Exists(r))
            {
                foreach (var f in ModuleSystem.EnumerateSourceFiles(r, isExcluded))
                {
                    if (seen.Add(f))
                    {
                        files.Add(f);
                    }
                }
            }
        }

        if (target is not null)
        {
            files = files.Where(f => f.StartsWith(target, StringComparison.Ordinal)).ToList();
            seen = [.. files];
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var stream = File.Create(outputPath);
        using var writer = new IndexWriter(stream);
        writer.WriteMetadata(new Metadata
        {
            ProjectRoot = new Uri(root + (root.EndsWith(Path.DirectorySeparatorChar) ? "" : Path.DirectorySeparatorChar)).AbsoluteUri.TrimEnd('/'),
            TextDocumentEncoding = TextEncoding.Utf8,
            ToolInfo = new ToolInfo { Name = "scip-python", Version = "0.6.8" },
        });

        var failures = 0;
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            var module = modules.Load(file);
            if (module is null)
            {
                continue;
            }

            if (target is null)
            {
                // Follow imports into project files that the include/exclude rules did not select.
                foreach (var imported in ImportedProjectFiles(module, modules))
                {
                    if (seen.Add(imported))
                    {
                        files.Add(imported);
                    }
                }
            }

            var doc = new Document
            {
                RelativePath = Path.GetRelativePath(indexRoot, file).Replace('\\', '/'),
                Language = "python",
                PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
            };
            try
            {
                new TreeVisitor(state, module, doc).Walk(module.Tree);
            }
            catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or NullReferenceException or ArgumentException)
            {
                failures++;
                Console.Error.WriteLine($"scip-python: failed to index {file}: {e.Message}");
                continue;
            }

            if (doc.Occurrences.Count == 0)
            {
                continue;
            }

            writer.WriteDocument(doc);
        }

        foreach (var ext in state.ExternalSymbols.Values)
        {
            writer.WriteExternalSymbol(ext);
        }

        if (!options.Quiet)
        {
            Console.Error.WriteLine($"Successfully wrote SCIP index to {outputPath} ({files.Count} files, {failures} failed)");
        }

        return 0;
    }

    private static IEnumerable<string> ImportedProjectFiles(SourceModule module, ModuleSystem modules)
    {
        var stack = new Stack<Syntax.Node>();
        stack.Push(module.Tree);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            foreach (var c in n.Children())
            {
                if (c is not null)
                {
                    stack.Push(c);
                }
            }

            IEnumerable<ResolvedModule?> resolved = n switch
            {
                Syntax.ImportAsNode ia => [modules.Resolve(ia.Module, module)],
                Syntax.ImportFromNode ifn => ResolveFrom(ifn, module, modules),
                _ => [],
            };
            foreach (var r in resolved)
            {
                if (r?.FilePath is { } p && modules.IsUnderProject(p))
                {
                    yield return p;
                }
            }
        }
    }

    private static IEnumerable<ResolvedModule?> ResolveFrom(Syntax.ImportFromNode node, SourceModule module, ModuleSystem modules)
    {
        yield return modules.Resolve(node.Module, module);
        var parts = node.Module.NameParts.Select(p => p.Value).ToList();
        foreach (var imp in node.Imports)
        {
            yield return modules.Resolve([.. parts, imp.Name.Value], node.Module.LeadingDots, module);
        }
    }
}

/// <summary>Finds site-packages directories without running Python (VIRTUAL_ENV, project venvs, common system locations).</summary>
internal static partial class EnvironmentDiscovery
{
    public static (List<string> SiteDirs, string? PythonVersion) Discover(string projectRoot)
    {
        var dirs = new List<string>();
        string? version = null;

        void AddVenv(string venv)
        {
            if (!Directory.Exists(venv))
            {
                return;
            }

            var cfg = Path.Combine(venv, "pyvenv.cfg");
            if (File.Exists(cfg))
            {
                foreach (var line in File.ReadLines(cfg))
                {
                    var m = VersionLine().Match(line);
                    if (m.Success)
                    {
                        version ??= m.Groups[1].Value;
                    }
                }
            }

            AddLibDirs(Path.Combine(venv, "lib"));
            var win = Path.Combine(venv, "Lib", "site-packages");
            if (Directory.Exists(win))
            {
                dirs.Add(win);
            }
        }

        void AddLibDirs(string lib)
        {
            if (!Directory.Exists(lib))
            {
                return;
            }

            foreach (var py in Directory.EnumerateDirectories(lib, "python3*").OrderBy(x => x, StringComparer.Ordinal))
            {
                foreach (var site in new[] { "site-packages", "dist-packages" })
                {
                    var d = Path.Combine(py, site);
                    if (Directory.Exists(d))
                    {
                        dirs.Add(d);
                        version ??= Path.GetFileName(py)["python".Length..];
                    }
                }
            }
        }

        var env = System.Environment.GetEnvironmentVariable("VIRTUAL_ENV");
        if (!string.IsNullOrEmpty(env))
        {
            AddVenv(env);
        }

        foreach (var name in new[] { ".venv", "venv", "env", ".env" })
        {
            AddVenv(Path.Combine(projectRoot, name));
        }

        if (dirs.Count == 0)
        {
            foreach (var lib in new[] { "/usr/local/lib", "/usr/lib" })
            {
                AddLibDirs(lib);
            }

            var home = System.Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                AddLibDirs(Path.Combine(home, ".local", "lib"));
            }
        }

        return (dirs.Distinct().ToList(), version is null ? null : string.Join('.', version.Split('.').Take(2)));
    }

    [GeneratedRegex(@"^\s*version(?:_info)?\s*=\s*(\d+\.\d+(?:\.\d+)?)")]
    private static partial Regex VersionLine();
}
