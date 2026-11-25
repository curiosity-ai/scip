using System.Text;

namespace ScipSharp;

/// <summary>
/// Configures how to format a SCIP symbol. Excluding parts of a symbol is useful for tests, e.g. snapshot
/// tests that don't want to hardcode package versions.
/// </summary>
public sealed class SymbolFormatter
{
    public Func<string, bool> IncludeScheme { get; init; } = static _ => true;
    public Func<string, bool> IncludePackageManager { get; init; } = static _ => true;
    public Func<string, bool> IncludePackageName { get; init; } = static _ => true;
    public Func<string, bool> IncludePackageVersion { get; init; } = static _ => true;
    public Func<string, bool> IncludeDescriptor { get; init; } = static _ => true;
    public Func<SymbolDescriptor, bool> IncludeRawDescriptor { get; init; } = static _ => true;
    public Func<string, bool> IncludeDisambiguator { get; init; } = static _ => true;

    /// <summary>When true, malformed symbols passed to <see cref="Format(string)"/> yield an empty string instead of throwing.</summary>
    public bool Lenient { get; init; }

    /// <summary>Formats every part of the symbol.</summary>
    public static SymbolFormatter Verbose { get; } = new();

    /// <summary>Like <see cref="Verbose"/> but silently ignores parse errors.</summary>
    public static SymbolFormatter LenientVerbose { get; } = new() { Lenient = true };

    /// <summary>Formats only the descriptor part of the symbol (and the scheme of local symbols).</summary>
    public static SymbolFormatter DescriptorOnly { get; } = new()
    {
        IncludeScheme = static scheme => scheme == "local",
        IncludePackageManager = static _ => false,
        IncludePackageName = static _ => false,
        IncludePackageVersion = static _ => false,
    };

    public string Format(string symbol)
    {
        if (!SymbolParser.TryParse(symbol, out var parsed, out var error))
        {
            return Lenient ? "" : throw new SymbolParseException(error);
        }

        return Format(parsed);
    }

    public string Format(Symbol symbol)
    {
        var b = new StringBuilder();
        if (IncludeScheme(symbol.Scheme))
        {
            WriteEscapedPackage(b, symbol.Scheme);
        }

        if (symbol.Package is { } pkg)
        {
            if (IncludePackageManager(pkg.Manager))
            {
                Separate(b);
                WriteEscapedPackage(b, pkg.Manager);
            }

            if (IncludePackageName(pkg.Name))
            {
                Separate(b);
                WriteEscapedPackage(b, pkg.Name);
            }

            if (IncludePackageVersion(pkg.Version))
            {
                Separate(b);
                WriteEscapedPackage(b, pkg.Version);
            }
        }

        var descriptors = FormatDescriptors(symbol.Descriptors);
        if (IncludeDescriptor(descriptors))
        {
            Separate(b);
            b.Append(descriptors);
        }

        return b.ToString();
    }

    public string FormatDescriptors(IEnumerable<SymbolDescriptor> descriptors)
    {
        var b = new StringBuilder();
        foreach (var d in descriptors)
        {
            if (!IncludeRawDescriptor(d))
            {
                continue;
            }

            switch (d.Suffix)
            {
                case SymbolDescriptor.Types.Suffix.Local: b.Append(d.Name); break;
                case SymbolDescriptor.Types.Suffix.Namespace: WriteSuffixed(b, d.Name, "/"); break;
                case SymbolDescriptor.Types.Suffix.Type: WriteSuffixed(b, d.Name, "#"); break;
                case SymbolDescriptor.Types.Suffix.Term: WriteSuffixed(b, d.Name, "."); break;
                case SymbolDescriptor.Types.Suffix.Meta: WriteSuffixed(b, d.Name, ":"); break;
                case SymbolDescriptor.Types.Suffix.Macro: WriteSuffixed(b, d.Name, "!"); break;
                case SymbolDescriptor.Types.Suffix.TypeParameter:
                    b.Append('[');
                    WriteSuffixed(b, d.Name, "]");
                    break;
                case SymbolDescriptor.Types.Suffix.Parameter:
                    b.Append('(');
                    WriteSuffixed(b, d.Name, ")");
                    break;
                case SymbolDescriptor.Types.Suffix.Method:
                    if (IncludeDisambiguator(d.Disambiguator))
                    {
                        WriteSuffixed(b, d.Name, "(");
                        WriteSuffixed(b, d.Disambiguator, ").");
                    }
                    else
                    {
                        WriteSuffixed(b, d.Name, "().");
                    }

                    break;
            }
        }

        return b.ToString();
    }

    private static void WriteEscapedPackage(StringBuilder b, string name) =>
        WriteEscaped(b, name.Length == 0 ? "." : name, ' ');

    private static void WriteSuffixed(StringBuilder b, string identifier, string suffix)
    {
        if (SymbolSyntax.IsSimpleIdentifier(identifier))
        {
            b.Append(identifier);
        }
        else
        {
            b.Append('`');
            WriteEscaped(b, identifier, '`');
            b.Append('`');
        }

        b.Append(suffix);
    }

    private static void WriteEscaped(StringBuilder b, string identifier, char escape)
    {
        foreach (var c in identifier)
        {
            b.Append(c);
            if (c == escape)
            {
                b.Append(c);
            }
        }
    }

    private static void Separate(StringBuilder b)
    {
        if (b.Length > 0)
        {
            b.Append(' ');
        }
    }
}
