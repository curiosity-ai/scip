using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Scip;

/// <summary>
/// Parses SCIP symbol strings (the grammar documented in <c>scip.proto</c>) into <see cref="Symbol"/> messages.
/// </summary>
public static class SymbolParser
{
    private const string LocalPrefix = "local ";

    /// <summary>Returns true if the symbol is obviously a local symbol. Does not fully validate the symbol.</summary>
    public static bool IsLocalSymbol(string symbol) => symbol.StartsWith(LocalPrefix, StringComparison.Ordinal);

    /// <summary>Returns true if the symbol is obviously not a local symbol. Does not fully validate the symbol.</summary>
    public static bool IsGlobalSymbol(string symbol) => !IsLocalSymbol(symbol);

    /// <summary>Parses <paramref name="symbol"/>, throwing <see cref="SymbolParseException"/> on failure.</summary>
    public static Symbol Parse(string symbol) =>
        TryParse(symbol, out var result, out var error) ? result : throw new SymbolParseException(error);

    /// <summary>Parses <paramref name="symbol"/> without throwing.</summary>
    public static bool TryParse(
        string symbol,
        [NotNullWhen(true)] out Symbol? result,
        [NotNullWhen(false)] out string? error)
    {
        var parsed = new Symbol();
        error = ParseInto(symbol, parsed, includeDescriptors: true);
        result = error is null ? parsed : null;
        return error is null;
    }

    /// <summary>Checks that <paramref name="symbol"/> is well-formed; returns the error message or <c>null</c>.</summary>
    public static string? Validate(string symbol) => ParseInto(symbol, null, includeDescriptors: true);

    private static string? ParseInto(string symbol, Symbol? output, bool includeDescriptors)
    {
        if (symbol.Length == 0)
        {
            return "empty symbol";
        }

        if (symbol.StartsWith(LocalPrefix, StringComparison.Ordinal))
        {
            var id = symbol.AsSpan(LocalPrefix.Length);
            if (id.IsEmpty || !SymbolSyntax.IsSimpleIdentifier(id))
            {
                return $"expected format 'local <simple-identifier>' but got: {symbol}";
            }

            if (output is not null)
            {
                output.Scheme = "local";
                output.Descriptors.Add(new SymbolDescriptor { Name = id.ToString(), Suffix = SymbolDescriptor.Types.Suffix.Local });
            }

            return null;
        }

        var reader = new Reader(symbol);
        if (reader.AcceptSpaceTerminated(Context.Scheme, out var scheme) is { } err)
        {
            return err;
        }

        if (reader.AcceptSpaceTerminated(Context.PackageManager, out var manager) is { } e1)
        {
            return e1;
        }

        if (reader.AcceptSpaceTerminated(Context.PackageName, out var name) is { } e2)
        {
            return e2;
        }

        if (reader.AcceptSpaceTerminated(Context.PackageVersion, out var version) is { } e3)
        {
            return e3;
        }

        if (output is not null)
        {
            output.Scheme = scheme;
            output.Package = new Package { Manager = NormalizeEmpty(manager), Name = NormalizeEmpty(name), Version = NormalizeEmpty(version) };
        }

        if (!includeDescriptors)
        {
            return null;
        }

        while (!reader.AtEnd)
        {
            if (reader.ParseDescriptor(out var descriptor) is { } e4)
            {
                return e4;
            }

            output?.Descriptors.Add(descriptor);
        }

        return null;
    }

    /// <summary>A lone <c>.</c> stands for the empty string in package fields.</summary>
    private static string NormalizeEmpty(string s) => s == "." ? "" : s;

    private enum Context
    {
        ParameterName,
        ClosingParameterName,
        TypeParameterName,
        ClosingTypeParameterName,
        DescriptorName,
        MethodDisambiguator,
        ClosingMethod,
        Scheme,
        PackageManager,
        PackageName,
        PackageVersion,
    }

    private static string Describe(Context c) => c switch
    {
        Context.ParameterName => "parameter name",
        Context.ClosingParameterName => "closing parameter name",
        Context.TypeParameterName => "type parameter name",
        Context.ClosingTypeParameterName => "closing type parameter name",
        Context.DescriptorName => "descriptor name",
        Context.MethodDisambiguator => "method disambiguator",
        Context.ClosingMethod => "closing method",
        Context.Scheme => "scheme",
        Context.PackageManager => "package manager",
        Context.PackageName => "package name",
        Context.PackageVersion => "package version",
        _ => "unknown",
    };

    // All grammar delimiters are ASCII, so scanning UTF-16 code units is equivalent to scanning code points.
    private ref struct Reader(string text)
    {
        private readonly string _s = text;
        private int _pos;

        public readonly bool AtEnd => _pos >= _s.Length;

        private readonly char Current => _pos < _s.Length ? _s[_pos] : '\0';

        private string FailEmpty(Context what) => $"failed to parse: \"{_s}\" (context: {Describe(what)})";

        private string FailByte(char expected, Context what) =>
            $"failed to parse: \"{_s}\" (expected byte: {expected}, context: {Describe(what)})";

        private string? AcceptChar(char c, Context what)
        {
            if (Current != c || AtEnd)
            {
                return FailByte(c, what);
            }

            _pos++;
            return null;
        }

        public string? AcceptSpaceTerminated(Context what, out string value) =>
            AcceptTerminated(what, ' ', out value);

        private string? AcceptIdentifier(Context what, out string value)
        {
            if (!AtEnd && Current == '`')
            {
                _pos++;
                return AcceptTerminated(what, '`', out value);
            }

            var start = _pos;
            while (_pos < _s.Length && SymbolSyntax.IsSimpleIdentifierCharacter(_s[_pos]))
            {
                _pos++;
            }

            value = _s[start.._pos];
            return start == _pos ? FailEmpty(what) : null;
        }

        /// <summary>
        /// Accepts text up to a single <paramref name="terminator"/>; a doubled terminator is a literal one.
        /// The terminator must be followed by at least one more character.
        /// </summary>
        private string? AcceptTerminated(Context what, char terminator, out string value)
        {
            value = "";
            StringBuilder? escaped = null;
            var anchor = _pos;
            while (_pos < _s.Length)
            {
                if (_s[_pos] != terminator)
                {
                    _pos++;
                    continue;
                }

                _pos++; // consume terminator
                if (_pos == _s.Length)
                {
                    break;
                }

                if (_s[_pos] == terminator)
                {
                    // Escaped terminator: keep one copy, skip the other.
                    (escaped ??= new StringBuilder()).Append(_s, anchor, _pos - anchor);
                    _pos++;
                    anchor = _pos;
                    continue;
                }

                var tail = _s.AsSpan(anchor, _pos - 1 - anchor);
                value = escaped is null ? tail.ToString() : escaped.Append(tail).ToString();
                return null;
            }

            return $"reached end of symbol while parsing <{Describe(what)}>, expected a '{terminator}' character";
        }

        public string? ParseDescriptor(out SymbolDescriptor descriptor)
        {
            descriptor = new SymbolDescriptor();
            var start = _pos;
            string? err;
            switch (Current)
            {
                case '(':
                    _pos++;
                    descriptor.Suffix = SymbolDescriptor.Types.Suffix.Parameter;
                    err = AcceptIdentifier(Context.ParameterName, out var p);
                    descriptor.Name = p;
                    return err ?? AcceptChar(')', Context.ClosingParameterName);
                case '[':
                    _pos++;
                    descriptor.Suffix = SymbolDescriptor.Types.Suffix.TypeParameter;
                    err = AcceptIdentifier(Context.TypeParameterName, out var t);
                    descriptor.Name = t;
                    return err ?? AcceptChar(']', Context.ClosingTypeParameterName);
            }

            if (AcceptIdentifier(Context.DescriptorName, out var name) is { } nameErr)
            {
                return nameErr;
            }

            descriptor.Name = name;
            var suffix = Current;
            if (!AtEnd)
            {
                _pos++;
            }

            switch (suffix)
            {
                case '(':
                    descriptor.Suffix = SymbolDescriptor.Types.Suffix.Method;
                    if (Current != ')')
                    {
                        if (AcceptIdentifier(Context.MethodDisambiguator, out var disambiguator) is { } dErr)
                        {
                            return dErr;
                        }

                        descriptor.Disambiguator = disambiguator;
                    }

                    return AcceptChar(')', Context.ClosingMethod) ?? AcceptChar('.', Context.ClosingMethod);
                case '/': descriptor.Suffix = SymbolDescriptor.Types.Suffix.Namespace; return null;
                case '.': descriptor.Suffix = SymbolDescriptor.Types.Suffix.Term; return null;
                case '#': descriptor.Suffix = SymbolDescriptor.Types.Suffix.Type; return null;
                case ':': descriptor.Suffix = SymbolDescriptor.Types.Suffix.Meta; return null;
                case '!': descriptor.Suffix = SymbolDescriptor.Types.Suffix.Macro; return null;
                default:
                    return $"unrecognized descriptor \"{_s[start..Math.Min(_pos, _s.Length)]}\"";
            }
        }
    }
}
