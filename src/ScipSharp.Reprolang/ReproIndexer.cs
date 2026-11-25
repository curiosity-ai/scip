using ScipIndex = ScipSharp.Index;

namespace ScipSharp.Reprolang;

/// <summary>A package whose sources are indexed only to resolve cross-project references.</summary>
public sealed record ReproDependency(string Name, string Version, IReadOnlyList<SourceFile> Sources)
{
    public ReproDependency(string name, IReadOnlyList<SourceFile> sources)
        : this(name, ReproIndexer.PackageVersion, sources)
    {
    }
}

/// <summary>Raised when reprolang names cannot be resolved; <see cref="Errors"/> lists every failure.</summary>
public sealed class ReproIndexException(IReadOnlyList<string> errors) : Exception(string.Join("\n", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>
/// Turns reprolang (<c>*.repro</c>) sources into a SCIP <see cref="ScipIndex"/>. Port of <c>reprolang/repro</c>.
/// </summary>
public static class ReproIndexer
{
    public const string PackageVersion = "1.0.0";
    public const string DependencyManager = "repro_manager";
    public const string FileExtension = ".repro";

    /// <summary>
    /// Indexes every file under <paramref name="directory"/> as package <c>basename(directory)</c>, using each
    /// sibling directory as a dependency (the layout of <c>testdata/snapshots/input</c>) when <paramref name="withSiblingDependencies"/> is set.
    /// </summary>
    public static ScipIndex Index(string directory, bool withSiblingDependencies = false)
    {
        var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        var name = Path.GetFileName(full);
        var dependencies = withSiblingDependencies ? SiblingDependencies(full) : [];
        return Index(new Uri(full + Path.DirectorySeparatorChar).AbsoluteUri.TrimEnd('/'), name, SourceFile.FromDirectory(full), dependencies);
    }

    /// <summary>The other directories next to <paramref name="directory"/>, each as a dependency named after the directory.</summary>
    public static List<ReproDependency> SiblingDependencies(string directory)
    {
        var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.GetDirectoryName(full)!;
        return Directory.EnumerateDirectories(root)
            .Where(d => !string.Equals(Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar), full, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(d => new ReproDependency(Path.GetFileName(d), SourceFile.FromDirectory(d)))
            .ToList();
    }

    /// <summary>The symbol formatter snapshot tests use: descriptors only, hiding the tested package's own name.</summary>
    public static SymbolFormatter SnapshotSymbolFormatter(string packageName) => new()
    {
        IncludeScheme = static scheme => scheme == "local",
        IncludePackageManager = static _ => false,
        IncludePackageName = name => name != packageName,
        IncludePackageVersion = static _ => false,
    };

    /// <summary>
    /// Indexes <paramref name="sources"/> (reprolang files) belonging to <paramref name="packageName"/>.
    /// </summary>
    /// <exception cref="ReproIndexException">Some names could not be resolved.</exception>
    public static ScipIndex Index(
        string projectRoot,
        string packageName,
        IReadOnlyList<SourceFile> sources,
        IReadOnlyList<ReproDependency>? dependencies = null)
    {
        var index = new ScipIndex
        {
            Metadata = new Metadata
            {
                Version = ProtocolVersion.UnspecifiedProtocolVersion,
                ToolInfo = new ToolInfo { Name = "reprolang", Version = "1.0.0", Arguments = { "arg1", "arg2" } },
                ProjectRoot = projectRoot,
                TextDocumentEncoding = TextEncoding.Utf8,
            },
        };

        var context = new Context(packageName);
        var files = sources.Select(s => new ParsedFile(s)).ToList();
        var dependencyFiles = (dependencies ?? [])
            .Select(d => (Dependency: d, Files: d.Sources.Select(s => new ParsedFile(s)).ToList()))
            .ToList();

        var errors = new List<string>();
        foreach (var (dependency, depFiles) in dependencyFiles)
        {
            EnterGlobalDefinitions(dependency, depFiles, context, errors);
        }

        foreach (var file in files)
        {
            EnterDefinitions(file, context);
        }

        foreach (var file in files)
        {
            ResolveReferences(file, context, errors);
        }

        if (errors.Count > 0)
        {
            throw new ReproIndexException(errors);
        }

        foreach (var file in files)
        {
            var document = new Document { RelativePath = file.Source.RelativePath.Replace('\\', '/') };
            document.Occurrences.AddRange(Occurrences(file));
            document.Symbols.AddRange(Symbols(file));
            index.Documents.Add(document);
        }

        return index;
    }

    private sealed class Context(string packageName)
    {
        public string PackageName { get; } = packageName;

        public Dictionary<string, string> GlobalScope { get; } = [];
    }

    private sealed class ParsedFile
    {
        public ParsedFile(SourceFile source)
        {
            Source = source;
            var parser = ReproParser.Parse(source.Text);
            Definitions = parser.Definitions;
            References = parser.References;
            Relationships = parser.Relationships;
            RelativePath = source.RelativePath.Replace('\\', '/');
        }

        public SourceFile Source { get; }

        public string RelativePath { get; }

        public List<DefinitionStatement> Definitions { get; }

        public List<ReferenceStatement> References { get; }

        public List<RelationshipsStatement> Relationships { get; }

        public Dictionary<string, string> LocalScope { get; } = [];
    }

    private static string NewGlobalSymbol(string packageName, string version, ParsedFile file, Identifier name) =>
        $"reprolang {DependencyManager} {packageName} {version} {file.RelativePath}/{name.Value}";

    private static void EnterGlobalDefinitions(ReproDependency dependency, List<ParsedFile> files, Context context, List<string> errors)
    {
        var formatter = SnapshotSymbolFormatter(context.PackageName);

        void Enter(ParsedFile file, Identifier name)
        {
            if (name.IsLocal)
            {
                return;
            }

            var symbol = NewGlobalSymbol(dependency.Name, dependency.Version, file, name);
            if (!SymbolParser.TryParse(symbol, out var parsed, out var error))
            {
                errors.Add($"package: {dependency.Name}: file: \"{file.Source.AbsolutePath}\": {error}");
                return;
            }

            context.GlobalScope["global " + formatter.Format(parsed)] = symbol;
        }

        foreach (var file in files)
        {
            foreach (var definition in file.Definitions)
            {
                Enter(file, definition.Name);
            }

            foreach (var relationship in file.Relationships)
            {
                Enter(file, relationship.Name);
            }
        }
    }

    private static void EnterDefinitions(ParsedFile file, Context context)
    {
        void Enter(Identifier name, Identifier defName)
        {
            var scope = name.IsLocal ? file.LocalScope : context.GlobalScope;
            var symbol = name.IsLocal
                ? $"local {defName.Value}"
                : NewGlobalSymbol(context.PackageName, PackageVersion, file, defName);
            name.Symbol = symbol;
            scope[name.Value] = symbol;
        }

        foreach (var definition in file.Definitions)
        {
            Enter(definition.Name, definition.Name);
        }

        foreach (var relationship in file.Relationships)
        {
            if (relationship.Relations.DefinedBy is { } definedBy)
            {
                Enter(relationship.Name, definedBy);
            }
        }
    }

    private static void ResolveReferences(ParsedFile file, Context context, List<string> errors)
    {
        var fileErrors = new List<string>();

        void Resolve(Identifier identifier)
        {
            var scope = identifier.IsLocal ? file.LocalScope : context.GlobalScope;
            if (scope.TryGetValue(identifier.Value, out var symbol))
            {
                identifier.Symbol = symbol;
            }
            else
            {
                fileErrors.Add($"could not resolve local symbol \"{identifier.Value}\"");
            }
        }

        foreach (var definition in file.Definitions)
        {
            definition.Relations.Identifiers().ToList().ForEach(Resolve);
        }

        foreach (var relationship in file.Relationships)
        {
            relationship.Relations.Identifiers().ToList().ForEach(Resolve);
        }

        foreach (var reference in file.References)
        {
            Resolve(reference.Name);
        }

        errors.AddRange(fileErrors.Select(e => $"file \"{file.Source.AbsolutePath}\": {e}"));
    }

    private static Occurrence MakeOccurrence(Identifier identifier, SymbolRole roles)
    {
        var occurrence = new Occurrence { Symbol = identifier.Symbol, SymbolRoles = (int)roles };
        if (identifier.Value.StartsWith("deprecated", StringComparison.Ordinal))
        {
            occurrence.Diagnostics.Add(new Diagnostic { Severity = Severity.Warning, Message = "deprecated identifier" });
        }

        occurrence.SetSourceRange(identifier.Position);
        return occurrence;
    }

    private static List<Occurrence> Occurrences(ParsedFile file)
    {
        var result = new List<Occurrence>();
        foreach (var definition in file.Definitions)
        {
            result.Add(MakeOccurrence(definition.Name, SymbolRole.Definition));
            result.AddRange(definition.Relations.Identifiers().Select(i => MakeOccurrence(i, SymbolRole.UnspecifiedSymbolRole)));
        }

        foreach (var relationship in file.Relationships)
        {
            result.AddRange(relationship.Relations.Identifiers().Select(i => MakeOccurrence(i, SymbolRole.UnspecifiedSymbolRole)));
        }

        foreach (var reference in file.References)
        {
            result.Add(MakeOccurrence(reference.Name, reference.IsForwardDef ? SymbolRole.ForwardDefinition : SymbolRole.UnspecifiedSymbolRole));
        }

        return result;
    }

    private static List<SymbolInformation> Symbols(ParsedFile file)
    {
        var result = new List<SymbolInformation>();
        foreach (var relationship in file.Relationships)
        {
            var info = new SymbolInformation { Symbol = relationship.Name.Symbol };
            info.Relationships.AddRange(ToRelationships(relationship.Relations));
            result.Add(info);
        }

        foreach (var definition in file.Definitions)
        {
            if (definition.Name.Value.Contains("NoSymbolInformation", StringComparison.Ordinal))
            {
                continue;
            }

            var info = new SymbolInformation { Symbol = definition.Name.Symbol };
            info.Documentation.Add("signature of " + definition.Name.Value);
            if (definition.Docstring.Length > 0)
            {
                info.Documentation.Add(definition.Docstring);
            }

            info.Relationships.AddRange(ToRelationships(definition.Relations));
            result.Add(info);
        }

        // Stable, so that the output is deterministic.
        return [.. result.OrderBy(s => s.Symbol, StringComparer.Ordinal)];
    }

    private static SortedDictionary<string, Relationship>.ValueCollection ToRelationships(RelationSet relations)
    {
        var bySymbol = new SortedDictionary<string, Relationship>(StringComparer.Ordinal);
        foreach (var identifier in relations.Identifiers())
        {
            bySymbol[identifier.Symbol] = new Relationship { Symbol = identifier.Symbol };
        }

        if (relations.Implements is { } implements) bySymbol[implements.Symbol].IsImplementation = true;
        if (relations.References is { } references) bySymbol[references.Symbol].IsReference = true;
        if (relations.TypeDefines is { } typeDefines) bySymbol[typeDefines.Symbol].IsTypeDefinition = true;
        if (relations.DefinedBy is { } definedBy) bySymbol[definedBy.Symbol].IsDefinition = true;
        return bySymbol.Values;
    }
}
