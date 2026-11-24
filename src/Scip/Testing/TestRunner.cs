#pragma warning disable CS0612 // deprecated range fields are intentionally supported

using System.Globalization;

namespace Scip.Testing;

/// <summary>
/// Validates that the SCIP data in an index matches human-readable test files
/// (port of <c>testutil/test_runner.go</c>; the core logic of <c>scip test</c>).
/// </summary>
public static class TestRunner
{
    /// <summary>Thrown by <see cref="Run"/> when at least one assertion failed or documents are missing.</summary>
    public sealed class TestFailedException() : Exception("test failed");

    /// <summary>Runs all assertions, writing progress to <paramref name="output"/>.</summary>
    /// <exception cref="TestFailedException">An assertion failed.</exception>
    public static void Run(
        string directory,
        IReadOnlyCollection<string> fileFilters,
        bool checkDocuments,
        Index index,
        string commentSyntax,
        TextWriter output)
    {
        var hasFailure = false;
        var filterSet = new HashSet<string>(fileFilters, StringComparer.Ordinal);
        var allTestFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in EnumerateEntries(directory))
        {
            if (Path.GetExtension(path) == ".scip")
            {
                continue;
            }

            if (filterSet.Count > 0)
            {
                if (filterSet.Contains(path))
                {
                    allTestFiles.Add(path);
                }
            }
            else if (!Directory.Exists(path))
            {
                allTestFiles.Add(path);
            }
        }

        foreach (var document in index.Documents)
        {
            var sourceFilePath = Path.Combine(directory, document.RelativePath);
            if (fileFilters.Count > 0 && !fileFilters.Contains(document.RelativePath))
            {
                continue;
            }

            var data = File.ReadAllText(sourceFilePath);
            allTestFiles.Remove(sourceFilePath);

            var failures = new List<string>();
            var successCount = 0;
            var syntheticDefinitions = SnapshotFormatter.SyntheticDefinitionsByParent(document.Symbols);
            var lines = data.Split('\n');

            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                var (cases, usedLines) = TestCasesForLine(lineNumber, lines, commentSyntax);
                if (cases.Count == 0)
                {
                    continue;
                }

                var attributes = AttributesForOccurrencesAtLine(
                    lineNumber, SnapshotFormatter.RenderLine(lines[lineNumber]), document.Occurrences, syntheticDefinitions);
                foreach (var testCase in cases)
                {
                    var filtered = FilterAttributesForTestCase(testCase, attributes);
                    if (!testCase.CheckAll(filtered))
                    {
                        failures.Add(FormatFailure(lineNumber, testCase, filtered));
                    }
                    else
                    {
                        successCount++;
                    }
                }

                lineNumber += usedLines;
            }

            if (failures.Count > 0)
            {
                hasFailure = true;
                output.Write(Colorize(output, "\u001b[31m", $"✗ {document.RelativePath}\n"));
                foreach (var failure in failures)
                {
                    output.Write($"{Indent(failure, 4)}\n");
                }
            }
            else
            {
                output.Write(Colorize(output, "\u001b[32m", $"✓ {document.RelativePath} ({successCount} assertions)\n"));
            }
        }

        if (checkDocuments && allTestFiles.Count > 0)
        {
            var sorted = allTestFiles.OrderBy(static f => f, StringComparer.Ordinal).ToList();
            output.Write(Colorize(output, "\u001b[31m", "✗ Missing documents in SCIP index\n"));
            foreach (var path in sorted)
            {
                output.Write($"    {path}\n");
            }

            hasFailure = true;
        }

        if (hasFailure)
        {
            throw new TestFailedException();
        }
    }

    private static IEnumerable<string> EnumerateEntries(string directory)
    {
        yield return directory;
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
        {
            yield return entry;
        }
    }

    /// <summary>Colors only when writing to an interactive console and NO_COLOR is unset (like fatih/color).</summary>
    private static string Colorize(TextWriter output, string code, string text)
    {
        if (!ReferenceEquals(output, Console.Out) || Console.IsOutputRedirected
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
        {
            return text;
        }

        var nl = text.EndsWith('\n') ? "\n" : "";
        return $"{code}{text.TrimEnd('\n')}\u001b[0m{nl}";
    }

    // ------------------------------- Model -------------------------------

    internal enum AttrKind
    {
        Definition,
        Reference,
        ForwardDefinition,
        SyntheticDefinition,
        Diagnostic,
    }

    internal static string KindName(AttrKind kind) => kind switch
    {
        AttrKind.Definition => "definition",
        AttrKind.Reference => "reference",
        AttrKind.ForwardDefinition => "forward_definition",
        AttrKind.SyntheticDefinition => "synthetic_definition",
        _ => "diagnostic",
    };

    internal static AttrKind KindFromString(string s) => s switch
    {
        "definition" => AttrKind.Definition,
        "reference" => AttrKind.Reference,
        "forward_definition" => AttrKind.ForwardDefinition,
        "synthetic_definition" => AttrKind.SyntheticDefinition,
        "diagnostic" => AttrKind.Diagnostic,
        _ => throw new ArgumentException($"Unknown symbolAttributeKind: {s}"),
    };

    /// <summary>A single attribute of a symbol: definition, reference, documentation or diagnostic.</summary>
    internal sealed class SymbolAttribute
    {
        public int Start { get; init; }

        public int Length { get; init; }

        public AttrKind Kind { get; init; }

        public string Data { get; init; } = "";

        public List<string> AdditionalData { get; set; } = [];

        public bool HasEndPosition { get; init; }

        public int EndLineDelta { get; init; }

        public int EndCharacter { get; init; }

        public string DisplayData() =>
            HasEndPosition ? string.Create(CultureInfo.InvariantCulture, $"{Data} {EndLineDelta}:{EndCharacter}") : Data;
    }

    internal sealed class TestCase(SymbolAttribute attribute, bool enforceLength)
    {
        public SymbolAttribute Attribute { get; } = attribute;

        public bool EnforceLength { get; } = enforceLength;

        public bool CheckAll(IEnumerable<SymbolAttribute> attributes) => attributes.Any(Check);

        public bool Check(SymbolAttribute attr)
        {
            if (EnforceLength)
            {
                if (Attribute.Length != attr.Length || Attribute.Start != attr.Start)
                {
                    return false;
                }
            }
            else if (Attribute.Start < attr.Start || Attribute.Start > attr.Start + attr.Length - 1)
            {
                return false;
            }

            if (Attribute.Kind != attr.Kind)
            {
                return false;
            }

            // A '.' part in the test symbol is a wildcard for the corresponding part.
            var expected = Fields(Attribute.Data);
            var actual = Fields(attr.Data);
            if (expected.Length != actual.Length)
            {
                return false;
            }

            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i] != "." && expected[i] != actual[i])
                {
                    return false;
                }
            }

            if (Attribute.HasEndPosition
                && (!attr.HasEndPosition || Attribute.EndLineDelta != attr.EndLineDelta || Attribute.EndCharacter != attr.EndCharacter))
            {
                return false;
            }

            if (Attribute.AdditionalData.Count > 0 && !Attribute.AdditionalData.SequenceEqual(attr.AdditionalData))
            {
                return false;
            }

            return true;
        }
    }

    private static string[] Fields(string s) => s.Split([' ', '\t', '\n', '\r', '\v', '\f'], StringSplitOptions.RemoveEmptyEntries);

    // ------------------------------- Parsing -------------------------------

    /// <summary>
    /// Returns the test cases in the comment block following <paramref name="lineNumber"/> and the number of lines they consume.
    /// </summary>
    internal static (List<TestCase> Cases, int UsedLines) TestCasesForLine(int lineNumber, IReadOnlyList<string> lines, string commentSyntax)
    {
        var testCases = new List<TestCase>();
        if (lineNumber >= lines.Count - 1)
        {
            return (testCases, 0);
        }

        var usedLines = 0;
        for (var i = lineNumber + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Trim().StartsWith(commentSyntax, StringComparison.Ordinal))
            {
                break;
            }

            if (!TryParseTestCase(line, lines.Skip(i + 1).ToList(), commentSyntax, out var testCase))
            {
                // Non-assertion comment line (e.g. snapshot metadata): consume and ignore.
                usedLines++;
                continue;
            }

            testCases.Add(testCase);
            i += testCase.Attribute.AdditionalData.Count;
            usedLines += 1 + testCase.Attribute.AdditionalData.Count;
        }

        return (testCases, usedLines);
    }

    internal static List<SymbolAttribute> FilterAttributesForTestCase(TestCase testCase, IEnumerable<SymbolAttribute> attributes) =>
        attributes.Where(a => testCase.Attribute.Start >= a.Start && testCase.Attribute.Start <= a.Start + a.Length - 1).ToList();

    private static SymbolAttribute AttributeForRange(Range pos, string renderedLine, AttrKind kind, string data) => new()
    {
        Start = pos.Start.Character,
        Length = SnapshotFormatter.MarkerLength(pos, renderedLine),
        Kind = kind,
        Data = data,
        HasEndPosition = !pos.IsSingleLine,
        EndLineDelta = pos.IsSingleLine ? 0 : pos.End.Line - pos.Start.Line,
        EndCharacter = pos.IsSingleLine ? 0 : pos.End.Character,
    };

    internal static List<SymbolAttribute> AttributesForOccurrencesAtLine(
        int lineNumber,
        string renderedLine,
        IEnumerable<Occurrence> occurrences,
        Dictionary<string, List<SymbolInformation>> syntheticDefinitions)
    {
        var result = new List<SymbolAttribute>();
        foreach (var occ in occurrences)
        {
            if (!occ.TryGetSourceRange(out var pos) || pos.Start.Line != lineNumber)
            {
                continue;
            }

            var isDefinition = SymbolRole.Definition.Matches(occ);
            var kind = isDefinition ? AttrKind.Definition
                : SymbolRole.ForwardDefinition.Matches(occ) ? AttrKind.ForwardDefinition
                : AttrKind.Reference;
            result.Add(AttributeForRange(pos, renderedLine, kind, occ.Symbol));

            foreach (var diagnostic in occ.Diagnostics)
            {
                var diag = AttributeForRange(pos, renderedLine, AttrKind.Diagnostic, diagnostic.Severity.ToString());
                diag.AdditionalData = [diagnostic.Message];
                result.Add(diag);
            }

            // Synthetic definitions are projected onto a real definition's range, mirroring `scip snapshot`.
            if (isDefinition && syntheticDefinitions.TryGetValue(occ.Symbol, out var synthetics))
            {
                foreach (var synthetic in synthetics)
                {
                    result.Add(AttributeForRange(pos, renderedLine, AttrKind.SyntheticDefinition, synthetic.Symbol));
                }
            }
        }

        return result;
    }

    private readonly record struct RangeSelection(int Start, int Length, bool EnforceLength, char Marker, string Content);

    /// <summary>
    /// Extracts the range-selection portion of a test comment line: <c>&lt;-</c>, a run of <c>^</c>, or a run of <c>_</c>
    /// (synthetic definitions). Returns false for lines without a range selection (e.g. snapshot metadata lines).
    /// </summary>
    private static bool TryParseRangeSelection(string line, string commentSyntax, out RangeSelection selection)
    {
        selection = default;
        var commentIdx = line.IndexOf(commentSyntax, StringComparison.Ordinal);
        if (commentIdx < 0)
        {
            return false;
        }

        var body = line[(commentIdx + commentSyntax.Length)..];
        var trimmed = body.TrimStart(' ', '\t');
        var leading = body.Length - trimmed.Length;

        if (trimmed.StartsWith("<-", StringComparison.Ordinal))
        {
            selection = new RangeSelection(commentIdx, 0, false, '^', trimmed[2..]);
            return true;
        }

        if (trimmed.Length == 0 || trimmed[0] is not ('^' or '_'))
        {
            return false;
        }

        var marker = trimmed[0];
        var runLen = 0;
        while (runLen < trimmed.Length && trimmed[runLen] == marker)
        {
            runLen++;
        }

        // A single marker ignores length; two or more enforce it.
        selection = new RangeSelection(
            commentIdx + commentSyntax.Length + leading,
            runLen >= 2 ? runLen : 0,
            runLen >= 2,
            marker,
            trimmed[runLen..]);
        return true;
    }

    /// <summary>Splits a trailing <c>&lt;lineDelta&gt;:&lt;endCharacter&gt;</c> token off the data (only when lineDelta &gt; 0).</summary>
    private static bool TryParseMultilineSuffix(string data, out string rest, out int endLineDelta, out int endCharacter)
    {
        rest = data;
        endLineDelta = endCharacter = 0;
        var fields = Fields(data);
        if (fields.Length == 0)
        {
            return false;
        }

        var last = fields[^1];
        var colon = last.IndexOf(':');
        if (colon <= 0 || colon == last.Length - 1)
        {
            return false;
        }

        if (!int.TryParse(last.AsSpan(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta)
            || !int.TryParse(last.AsSpan(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var endChar)
            || delta <= 0 || endChar < 0)
        {
            return false;
        }

        var trimmedData = data.TrimEnd();
        rest = trimmedData[..^last.Length].Trim();
        endLineDelta = delta;
        endCharacter = endChar;
        return true;
    }

    /// <summary>Parses one test comment line; false for comment lines that are not assertions.</summary>
    internal static bool TryParseTestCase(string line, IReadOnlyList<string> leadingLines, string commentSyntax, out TestCase testCase)
    {
        testCase = null!;
        if (!TryParseRangeSelection(line, commentSyntax, out var sel))
        {
            return false;
        }

        var content = sel.Content.Trim();
        var fields = Fields(content);
        if (fields.Length == 0)
        {
            return false;
        }

        var kindStr = fields[0];
        var kind = KindFromString(kindStr);
        if (sel.Marker == '_' && kind != AttrKind.SyntheticDefinition)
        {
            throw new InvalidOperationException($"'_' range marker is only valid for synthetic_definition, got \"{kindStr}\"");
        }

        var data = content[kindStr.Length..].Trim();
        var hasEnd = TryParseMultilineSuffix(data, out data, out var endLineDelta, out var endCharacter);

        var additional = new List<string>();
        foreach (var raw in leadingLines)
        {
            if (!raw.Trim().StartsWith(commentSyntax, StringComparison.Ordinal))
            {
                break;
            }

            var stripped = ReplaceFirst(raw, commentSyntax, "");
            if (!stripped.Trim().StartsWith('>'))
            {
                break;
            }

            additional.Add(ReplaceFirst(stripped, ">", "").Trim());
        }

        testCase = new TestCase(
            new SymbolAttribute
            {
                Kind = kind,
                Start = sel.Start,
                Length = sel.Length,
                Data = data,
                AdditionalData = additional,
                HasEndPosition = hasEnd,
                EndLineDelta = endLineDelta,
                EndCharacter = endCharacter,
            },
            sel.EnforceLength);
        return true;
    }

    private static string ReplaceFirst(string s, string search, string replacement)
    {
        var i = s.IndexOf(search, StringComparison.Ordinal);
        return i < 0 ? s : string.Concat(s.AsSpan(0, i), replacement, s.AsSpan(i + search.Length));
    }

    // ------------------------------- Reporting -------------------------------

    internal static string FormatFailure(int lineNumber, TestCase testCase, IReadOnlyList<SymbolAttribute> attributesAtLine)
    {
        var a = testCase.Attribute;
        var desc = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"Failure [Ln: {lineNumber + 1}, Col: {a.Start}]"),
            $"  Expected: '{KindName(a.Kind)} {a.DisplayData()}'",
        };
        desc.AddRange(a.AdditionalData.Select(static add => Indent($"'{add}'", 12)));

        if (attributesAtLine.Count == 0)
        {
            desc.Add("    Actual: <no attributes found>");
        }
        else
        {
            for (var i = 0; i < attributesAtLine.Count; i++)
            {
                var attr = attributesAtLine[i];
                var prefix = i == 0 ? "Actual:" : "       ";
                prefix += attributesAtLine.Count > 1 ? " *" : "  ";
                desc.Add($"  {prefix} '{KindName(attr.Kind)} {attr.DisplayData()}'");
                desc.AddRange(attr.AdditionalData.Select(static add => Indent($"'{add}'", 12)));
            }
        }

        return string.Join('\n', desc);
    }

    internal static string Indent(string str, int count)
    {
        var pad = new string(' ', count);
        return string.Join('\n', str.Split('\n').Select(l => pad + l));
    }
}
