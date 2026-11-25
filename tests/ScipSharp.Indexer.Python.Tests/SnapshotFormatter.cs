using System.Text;

namespace ScipSharp.Indexer.Python.Tests;

/// <summary>Port of <c>formatSnapshot</c> from scip-python's <c>lib.ts</c>: renders a document as annotated source.</summary>
internal static class SnapshotFormatter
{
    private const string PackageName = "scip-python python";
    private const string CommentSyntax = "#";
    private const string FormatOptionsPrefix = "# format-options:";

    private readonly record struct Rng(int StartLine, int StartChar, int EndLine, int EndChar)
    {
        public static Rng From(IList<int> r) =>
            r.Count == 3 ? new Rng(r[0], r[1], r[0], r[2]) : new Rng(r[0], r[1], r[2], r[3]);

        public int Compare(Rng o)
        {
            var c = StartLine.CompareTo(o.StartLine);
            if (c != 0)
            {
                return c;
            }

            c = StartChar.CompareTo(o.StartChar);
            if (c != 0)
            {
                return c;
            }

            c = EndLine.CompareTo(o.EndLine);
            return c != 0 ? c : EndChar.CompareTo(o.EndChar);
        }
    }

    public static string Format(string input, Document doc, IEnumerable<SymbolInformation> externalSymbols)
    {
        var lines = input.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var showDocs = false;
        var showRanges = false;
        foreach (var line in lines)
        {
            if (!line.StartsWith(FormatOptionsPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var opt in line[FormatOptionsPrefix.Length..].Trim().Split(','))
            {
                switch (opt.Trim())
                {
                    case "showDocs": showDocs = true; break;
                    case "showRanges": showRanges = true; break;
                    default: throw new InvalidOperationException("Invalid format option: " + opt);
                }
            }

            break;
        }

        var symbolTable = doc.Symbols.GroupBy(s => s.Symbol).ToDictionary(g => g.Key, g => g.Last());
        var externalTable = externalSymbols.GroupBy(s => s.Symbol).ToDictionary(g => g.Key, g => g.Last());
        var withDefinition = doc.Occurrences.Where(o => (o.SymbolRoles & (int)SymbolRole.Definition) > 0).Select(o => o.Symbol).ToHashSet();

        var enclosing = new List<(Rng Range, string Symbol)>();
        if (showRanges)
        {
            enclosing.AddRange(doc.Occurrences.Where(o => o.EnclosingRange.Count > 0).Select(o => (Rng.From(o.EnclosingRange), o.Symbol)));
        }

        var enclosingComparer = Comparer<Rng>.Create((a, b) =>
        {
            var c = a.Compare(b);
            return c != 0 ? c : b.EndLine.CompareTo(a.EndLine);
        });
        enclosing = [.. enclosing.Select((e, i) => (e, i)).OrderBy(x => x.e.Range, enclosingComparer).ThenBy(x => x.i).Select(x => x.e)];

        var starts = Enumerable.Range(0, lines.Count).Select(_ => new List<(Rng Range, string Symbol)>()).ToList();
        var ends = Enumerable.Range(0, lines.Count).Select(_ => new List<(Rng Range, string Symbol)>()).ToList();
        foreach (var e in enclosing)
        {
            starts[e.Range.StartLine].Add(e);
            ends[e.Range.EndLine].Insert(0, e);
        }

        var emitted = new HashSet<string>();
        var sb = new StringBuilder();

        void PushDoc(Rng range, string symbol, bool isDefinition, bool isStartOfLine)
        {
            if (emitted.Contains(symbol) || !isDefinition && withDefinition.Contains(symbol))
            {
                sb.Append('\n');
                return;
            }

            emitted.Add(symbol);
            var prefix = "\n" + CommentSyntax;
            if (!isStartOfLine)
            {
                prefix += new string(' ', range.StartChar - 1);
            }

            void OneDoc(IEnumerable<string> docs, bool external)
            {
                if (!showDocs)
                {
                    return;
                }

                foreach (var documentation in docs)
                {
                    var idx = 0;
                    foreach (var line in documentation.Split('\n'))
                    {
                        sb.Append(prefix);
                        if (idx++ == 0)
                        {
                            sb.Append(external ? "external " : "").Append("documentation ");
                        }
                        else
                        {
                            sb.Append("            > ");
                        }

                        sb.Append(line.Length > 40 ? line[..40] : line);
                        if (line.Length > 40)
                        {
                            sb.Append("...");
                        }
                    }
                }
            }

            void Relationships(IEnumerable<Relationship> rels)
            {
                foreach (var r in rels.OrderBy(r => r.Symbol, StringComparer.Ordinal))
                {
                    sb.Append(prefix).Append("relationship");
                    if (r.IsImplementation)
                    {
                        sb.Append(" implementation");
                    }

                    if (r.IsReference)
                    {
                        sb.Append(" reference");
                    }

                    if (r.IsTypeDefinition)
                    {
                        sb.Append(" type_definition");
                    }

                    sb.Append(' ').Append(r.Symbol);
                }
            }

            if (externalTable.TryGetValue(symbol, out var ext))
            {
                OneDoc(ext.Documentation, true);
                Relationships(ext.Relationships);
            }
            else if (symbolTable.TryGetValue(symbol, out var info))
            {
                OneDoc(info.Documentation, false);
                Relationships(info.Relationships);
            }

            sb.Append('\n');
        }

        void PushEnclosing((Rng Range, string Symbol) e, bool end)
        {
            if (!showRanges)
            {
                return;
            }

            sb.Append(CommentSyntax).Append(new string(' ', Math.Max(1, e.Range.StartChar - 1)));
            sb.Append(e.Range.StartChar < 2 ? "<" : end ? "^" : "⌄");
            sb.Append(end ? " end " : " start ").Append("enclosing_range ").Append(e.Symbol).Append('\n');
        }

        var occurrences = doc.Occurrences.Select((o, i) => (o, i))
            .OrderBy(x => x.o, Comparer<Occurrence>.Create((a, b) => Rng.From(a.Range).Compare(Rng.From(b.Range))))
            .ThenBy(x => x.i).Select(x => x.o).ToList();
        var oi = 0;
        for (var ln = 0; ln < lines.Count; ln++)
        {
            if (oi == 0 && occurrences.Count > 0)
            {
                var occ = occurrences[0];
                var r = Rng.From(occ.Range);
                if (r.StartChar == 0 && r.EndChar == 0)
                {
                    var isDef = (occ.SymbolRoles & (int)SymbolRole.Definition) > 0;
                    sb.Append(CommentSyntax).Append(" < ").Append(isDef ? "definition" : "reference").Append(' ').Append(occ.Symbol);
                    PushDoc(r, occ.Symbol, isDef, true);
                    sb.Append('\n');
                    oi++;
                }
            }

            foreach (var e in starts[ln])
            {
                PushEnclosing(e, false);
            }

            sb.Append(lines[ln]).Append('\n');
            while (oi < occurrences.Count && occurrences[oi].Range[0] == ln)
            {
                var occ = occurrences[oi++];
                if (occ.Range.Count > 3)
                {
                    throw new NotSupportedException("multi-line occurrence");
                }

                var range = Rng.From(occ.Range);
                sb.Append(CommentSyntax);
                var startOfLine = range.StartChar == 0;
                if (!startOfLine)
                {
                    sb.Append(' ', range.StartChar - 1);
                }

                var caret = range.EndChar - range.StartChar - (startOfLine ? 1 : 0);
                sb.Append('^', Math.Max(0, caret)).Append(' ');
                var isDef = (occ.SymbolRoles & (int)SymbolRole.Definition) > 0;
                sb.Append(isDef ? "definition" : "reference").Append(' ');
                var symbol = occ.Symbol.StartsWith(PackageName, StringComparison.Ordinal) ? occ.Symbol[PackageName.Length..] : occ.Symbol;
                sb.Append(symbol.Replace("\n", "|"));
                PushDoc(range, occ.Symbol, isDef, startOfLine);
            }

            foreach (var e in ends[ln])
            {
                PushEnclosing(e, true);
            }
        }

        return sb.ToString();
    }
}
