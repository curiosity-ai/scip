using System.Globalization;

namespace Scip.Indexer.Java;

/// <summary>Kinds of descriptors that scip-java's bare (package-less) symbols are composed of.</summary>
public enum DescriptorKind { None, Local, Term, Method, Type, Package, Parameter, TypeParameter }

/// <summary>One descriptor of a bare symbol, e.g. <c>Test#</c> or <c>m1().</c>.</summary>
public sealed record JavaDescriptor(DescriptorKind Kind, string Name, string Disambiguator = "")
{
    public static JavaDescriptor None { get; } = new(DescriptorKind.None, "");

    public string Encode() => Kind switch
    {
        DescriptorKind.None => "",
        DescriptorKind.Term => JavaSymbols.EncodeName(Name) + ".",
        DescriptorKind.Method => JavaSymbols.EncodeName(Name) + Disambiguator + ".",
        DescriptorKind.Type => JavaSymbols.EncodeName(Name) + "#",
        DescriptorKind.Package => JavaSymbols.EncodeName(Name) + "/",
        DescriptorKind.Parameter => "(" + JavaSymbols.EncodeName(Name) + ")",
        DescriptorKind.TypeParameter => "[" + JavaSymbols.EncodeName(Name) + "]",
        _ => throw new ArgumentException(Kind.ToString()),
    };
}

/// <summary>A parsed bare symbol: its last descriptor and the (bare) symbol of its owner.</summary>
public sealed record BareSymbol(JavaDescriptor Descriptor, string Owner)
{
    public static BareSymbol None { get; } = new(JavaDescriptor.None, JavaSymbols.None);
}

/// <summary>Port of <c>ScipSymbols.java</c> + <c>SymbolDescriptor.java</c> (bare Java SCIP symbols).</summary>
public static class JavaSymbols
{
    public const string None = "";
    public const string RootPackage = "_root_/";

    public static string Global(string owner, JavaDescriptor desc) =>
        desc.Kind == DescriptorKind.None ? None : owner == RootPackage ? desc.Encode() : owner + desc.Encode();

    public static string Local(int suffix) => "local " + suffix.ToString(CultureInfo.InvariantCulture);

    public static bool IsLocal(string symbol) => symbol.StartsWith("local ", StringComparison.Ordinal);

    public static bool IsGlobal(string symbol) => !IsLocal(symbol);

    /// <summary>Wraps non-identifier names in backticks, mirroring the upstream rules.</summary>
    public static string EncodeName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "``";
        if (!IsJavaIdentifierStart(name[0])) return "`" + name + "`";
        for (var i = 1; i < name.Length; i++)
        {
            if (!IsJavaIdentifierPart(name[i])) return "`" + name + "`";
        }
        return name;
    }

    /// <summary>Approximation of <c>Character.isJavaIdentifierStart</c>.</summary>
    public static bool IsJavaIdentifierStart(char c) =>
        char.IsLetter(c) || c == '_' || CharUnicodeInfo.GetUnicodeCategory(c) is
            UnicodeCategory.CurrencySymbol or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.LetterNumber;

    /// <summary>Approximation of <c>Character.isJavaIdentifierPart</c>.</summary>
    public static bool IsJavaIdentifierPart(char c) =>
        IsJavaIdentifierStart(c) || char.IsDigit(c) || CharUnicodeInfo.GetUnicodeCategory(c) is
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            || c <= '\u0008' || (c >= '\u000E' && c <= '\u001B') || (c >= '\u007F' && c <= '\u009F');

    /// <summary>Parses the last descriptor of a bare symbol.</summary>
    public static BareSymbol Parse(string symbol)
    {
        if (IsLocal(symbol)) return new BareSymbol(new JavaDescriptor(DescriptorKind.Local, symbol), None);
        if (symbol == None) return BareSymbol.None;
        var p = new ReverseParser(symbol);
        var descriptor = p.ParseDescriptor();
        var owner = p.Index < 0 ? RootPackage : symbol[..(p.Index + 1)];
        return new BareSymbol(descriptor, owner);
    }

    /// <summary>The top-level (direct child of a package) symbol that contains <paramref name="symbol"/>.</summary>
    public static BareSymbol? TopLevel(string? symbol)
    {
        if (string.IsNullOrEmpty(symbol) || IsLocal(symbol)) return null;
        var current = Parse(symbol);
        while (true)
        {
            if (current.Descriptor.Kind == DescriptorKind.Package) return null;
            var owner = Parse(current.Owner);
            if (owner.Descriptor.Kind == DescriptorKind.Package) return current;
            current = owner;
        }
    }

    private sealed class ReverseParser
    {
        private const char Bof = '\u0000';
        private readonly string _symbol;
        private char _curr;

        public ReverseParser(string symbol)
        {
            _symbol = symbol;
            Index = symbol.Length;
            ReadChar();
        }

        public int Index { get; private set; }

        private FormatException Fail() =>
            new($"invalid symbol format\n{_symbol}\n{new string(' ', Math.Max(Index, 0))}^");

        private char ReadChar()
        {
            if (Index < 0) throw Fail();
            if (Index == 0)
            {
                Index--;
                return _curr = Bof;
            }
            Index--;
            return _curr = _symbol[Index];
        }

        private string ParseValue()
        {
            if (_curr == '`')
            {
                var end = Index;
                while (ReadChar() != '`') { }
                ReadChar();
                return _symbol[(Index + 2)..end];
            }

            var stop = Index + 1;
            if (!IsJavaIdentifierPart(_curr)) throw Fail();
            while (IsJavaIdentifierPart(ReadChar()) && _curr != Bof) { }
            return _symbol[(Index + 1)..stop];
        }

        private string ParseDisambiguator()
        {
            var end = Index + 1;
            if (_curr != ')') throw Fail();
            while (ReadChar() != '(') { }
            ReadChar();
            return _symbol[(Index + 1)..end];
        }

        public JavaDescriptor ParseDescriptor()
        {
            string value;
            switch (_curr)
            {
                case '.':
                    ReadChar();
                    if (_curr == ')')
                    {
                        var dis = ParseDisambiguator();
                        value = ParseValue();
                        return new JavaDescriptor(DescriptorKind.Method, value, dis);
                    }
                    return new JavaDescriptor(DescriptorKind.Term, ParseValue());
                case '#':
                    ReadChar();
                    return new JavaDescriptor(DescriptorKind.Type, ParseValue());
                case '/':
                    ReadChar();
                    return new JavaDescriptor(DescriptorKind.Package, ParseValue());
                case ')':
                    ReadChar();
                    value = ParseValue();
                    if (_curr != '(') throw Fail();
                    ReadChar();
                    return new JavaDescriptor(DescriptorKind.Parameter, value);
                case ']':
                    ReadChar();
                    value = ParseValue();
                    if (_curr != '[') throw Fail();
                    ReadChar();
                    return new JavaDescriptor(DescriptorKind.TypeParameter, value);
                default:
                    throw Fail();
            }
        }
    }
}
