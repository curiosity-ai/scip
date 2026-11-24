namespace Scip.Indexer.Dart;

/// <summary>
/// Generates the SCIP document for <c>pubspec.yaml</c> (<c>--index-pubspec</c>): a definition of the package itself
/// (unless <c>publish_to: none</c>) and a reference per declared dependency, resolved through <c>pubspec.lock</c>.
/// </summary>
internal static class PubspecIndexer
{
    private static readonly string[] SectionOrder = Pubspec.DependencySections;

    public static Document Index(Pubspec pubspec, string text, IReadOnlyDictionary<string, string> lockVersions)
    {
        var lines = new LineIndex(text);
        var document = new Document { Language = "YAML", RelativePath = "pubspec.yaml", PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart };

        if (pubspec.PublishTo != "none")
        {
            var symbol = DependencySymbol(pubspec.Name, pubspec.Version ?? "*");
            document.Symbols.Add(new SymbolInformation
            {
                Symbol = symbol,
                SignatureDocumentation = new Signature
                {
                    Language = "YAML",
                    Text = $"name: {pubspec.Name}\nversion: {pubspec.Version ?? "null"}",
                },
            });
            document.Occurrences.Add(new Occurrence
            {
                Symbol = symbol,
                SymbolRoles = (int)SymbolRole.Definition,
                SingleLineRange = new SingleLineRange(),
            });
        }

        foreach (var section in SectionOrder)
        {
            foreach (var dep in pubspec.Dependencies[section])
            {
                if (!lockVersions.TryGetValue(dep, out var version))
                {
                    throw new InvalidOperationException($"Unable to find {dep} in pubspec.lock. Have you ran pub get?");
                }

                var symbol = DependencySymbol(dep, version);
                document.Symbols.Add(new SymbolInformation
                {
                    DisplayName = dep,
                    Symbol = symbol,
                    SignatureDocumentation = new Signature { Language = "YAML", Text = $"name: {dep}\nversion: {version}" },
                });

                var offset = DependencyOffset(text, section, dep)
                    ?? throw new InvalidOperationException($"Unable to find section \"{section}:\" in pubspec.yaml");
                var r = lines.RangeOf(offset, dep.Length);
                document.Occurrences.Add(new Occurrence
                {
                    Symbol = symbol,
                    SingleLineRange = new SingleLineRange { Line = r.StartLine, StartCharacter = r.StartCharacter, EndCharacter = r.EndCharacter },
                });
            }
        }

        return document;
    }

    private static string DependencySymbol(string name, string version)
    {
        var symbol = new Symbol { Scheme = "scip-dart", Package = new Package { Manager = "pub", Name = name, Version = version } };
        symbol.Descriptors.Add(new SymbolDescriptor { Name = "pubspec.yaml", Suffix = SymbolDescriptor.Types.Suffix.Namespace });
        return SymbolFormatter.Verbose.Format(symbol);
    }

    private static int? DependencyOffset(string text, string section, string dep)
    {
        var pos = 0;
        var inSection = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith(section + ":", StringComparison.Ordinal))
            {
                inSection = true;
            }
            else if (inSection && line.Trim().Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                inSection = false;
            }
            else if (inSection)
            {
                var indent = line.Length - line.TrimStart().Length;
                if (indent > 0 && line.AsSpan(indent).StartsWith(dep + ":", StringComparison.Ordinal))
                {
                    return pos + indent;
                }
            }

            pos += raw.Length + 1;
        }

        return null;
    }
}
