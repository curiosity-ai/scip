// Ported from abaplint packages/core/src/abap/2_statements/combi.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
//
// The combinator library. Statement and expression definitions use the factory functions on the
// static class Combi through "using static ScipSharp.Indexer.Abap.Statements.Combi;", so that
//   seq(Source, altPrio("?TO", "TO"), Target)          (TS)
// becomes
//   Seq(Expr<Source>(), AltPrio("?TO", "TO"), Expr<Target>())   (C#)
// Every combinator argument is an "input": a string (keyword or keyword sequence), an
// IStatementRunnable (another combinator, or an Expression instance) or a System.Type of an
// Expression subclass. See PORTING.md for the full translation table.
using System.Collections.Concurrent;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Statements;

using SysRegex = System.Text.RegularExpressions.Regex;
using SysRegexOptions = System.Text.RegularExpressions.RegexOptions;

internal static class CombiShared
{
    // shared result for "nothing matched". Returned lists are never mutated by callers,
    // which allows returning shared/input lists as is
    public static readonly IReadOnlyList<Result> EMPTY = Array.Empty<Result>();
}

internal sealed class RegexCombinator : IStatementRunnable
{
    private readonly SysRegex regexp;
    private readonly string source;
    private readonly string flags;

    public RegexCombinator(SysRegex r, string source, string flags)
    {
        regexp = r;
        this.source = source;
        this.flags = flags;
    }

    public List<string> ListKeywords() => [];

    public List<string> GetUsing() => [];

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        List<Result>? result = null;

        foreach (var input in r)
        {
            if (input.RemainingLength() == 0)
            {
                continue;
            }
            var token = input.Peek();
            if (regexp.IsMatch(token.GetStr()))
            {
                var shifted = input.Shift(new TokenNodeRegex(token));
                (result ??= []).Add(shifted);
            }
        }

        return result ?? CombiShared.EMPTY;
    }

    public string Railroad() => "Railroad.Terminal(\"" + source.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\")";

    public string ToStr() => "/" + source + "/" + flags;

    public List<string> First() => [""];
}

internal sealed class Word : IStatementRunnable
{
    private readonly string s;

    public Word(string s)
    {
        this.s = JsString.ToUpper(s);
    }

    public List<string> ListKeywords() => [s];

    public List<string> GetUsing() => [];

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        List<Result>? result = null;

        foreach (var input in r)
        {
            if (input.RemainingLength() != 0
                && input.Peek().GetUpperStr() == s)
            {
                var shifted = input.Shift(new TokenNode(input.Peek()));
                (result ??= []).Add(shifted);
            }
        }
        return result ?? CombiShared.EMPTY;
    }

    public string Railroad() => "Railroad.Terminal('\"" + s + "\"')";

    public string ToStr() => "\"" + s + "\"";

    public List<string> First() => [s];
}

internal sealed class TokenCombinator : IStatementRunnable
{
    private readonly string name;
    private readonly Type tokenType;

    public TokenCombinator(Type t)
    {
        if (!typeof(AbstractToken).IsAssignableFrom(t))
        {
            throw new ArgumentException("Tok: not a token type, " + t.Name);
        }
        tokenType = t;
        name = t.Name;
    }

    public List<string> ListKeywords() => [];

    public List<string> GetUsing() => [];

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        List<Result>? result = null;

        foreach (var input in r)
        {
            if (input.RemainingLength() != 0
                && input.Peek().GetType() == tokenType)
            {
                var shifted = input.Shift(new TokenNode(input.Peek()));
                (result ??= []).Add(shifted);
            }
        }
        return result ?? CombiShared.EMPTY;
    }

    public string Railroad()
    {
        // faithful to upstream: the TS lookup compares an upper cased export name with the class
        // name, which never matches, so the class name is used
        var text = name;
        return "Railroad.Terminal('!\"" + text + "\"')";
    }

    public string ToStr() => "Token \"" + name + "\"";

    public List<string> First() => [""];
}

public enum AlsoIn
{
    OpenABAP,
}

internal sealed class Vers : IStatementRunnable
{
    private readonly ABAPRelease release;
    private readonly IStatementRunnable runnable;
    private readonly AlsoIn? also;

    public Vers(ABAPRelease release, IStatementRunnable runnable, AlsoIn? also = null)
    {
        this.release = release;
        this.runnable = runnable;
        this.also = also;
    }

    public List<string> ListKeywords() => runnable.ListKeywords();

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        if (Combi.IsOpenABAP())
        {
            if (also == AlsoIn.OpenABAP) { return runnable.Run(r); }
            return Versions.ReleaseAtLeast(Release.v702, release) ? runnable.Run(r) : CombiShared.EMPTY;
        }

        return Versions.ReleaseAtLeast(Combi.GetRelease(), release) ? runnable.Run(r) : CombiShared.EMPTY;
    }

    public List<string> GetUsing() => runnable.GetUsing();

    public string Railroad()
    {
        var text = release.Abap ?? "Newest";
        if (also != null)
        {
            text += " or " + also;
        }
        return "Railroad.Sequence(Railroad.Comment(\"" + text + "\", {}), " + runnable.Railroad() + ")";
    }

    public string ToStr() => "Version(" + runnable.ToStr() + ")";

    public List<string> First() => runnable.First();
}

internal sealed class LangVers : IStatementRunnable
{
    private readonly LanguageVersion langVersion;
    private readonly IStatementRunnable runnable;

    public LangVers(LanguageVersion langVersion, IStatementRunnable runnable)
    {
        this.langVersion = langVersion;
        this.runnable = runnable;
    }

    public List<string> ListKeywords() => runnable.ListKeywords();

    public List<string> GetUsing() => runnable.GetUsing();

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        if (Combi.LangVersionOrdinal(Combi.GetLanguageVersion()) <= Combi.LangVersionOrdinal(langVersion))
        {
            return runnable.Run(r);
        }
        return CombiShared.EMPTY;
    }

    public string Railroad() => "Railroad.Sequence(Railroad.Comment(\"langver=" + langVersion + "\", {}), " + runnable.Railroad() + ")";

    public string ToStr() => "LangVersion(" + runnable.ToStr() + ")";

    public List<string> First() => runnable.First();
}

internal sealed class LangVersNot : IStatementRunnable
{
    private readonly LanguageVersion langVersion;
    private readonly IStatementRunnable runnable;

    public LangVersNot(LanguageVersion langVersion, IStatementRunnable runnable)
    {
        this.langVersion = langVersion;
        this.runnable = runnable;
    }

    public List<string> ListKeywords() => runnable.ListKeywords();

    public List<string> GetUsing() => runnable.GetUsing();

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        if (Combi.LangVersionOrdinal(Combi.GetLanguageVersion()) > Combi.LangVersionOrdinal(langVersion))
        {
            return runnable.Run(r);
        }
        return CombiShared.EMPTY;
    }

    public string Railroad() => "Railroad.Sequence(Railroad.Comment(\"not langver=" + langVersion + "\", {}), " + runnable.Railroad() + ")";

    public string ToStr() => "LanguageVersionNot(" + runnable.ToStr() + ")";

    public List<string> First() => runnable.First();
}

internal sealed class OptionalPriority : IStatementRunnable
{
    private readonly IStatementRunnable optional;

    public OptionalPriority(IStatementRunnable optional)
    {
        this.optional = optional;
    }

    public List<string> ListKeywords() => optional.ListKeywords();

    public List<string> GetUsing() => optional.GetUsing();

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        if (r.Count == 1)
        {
            // single input, pass and return the input list without wrapping
            var res = optional.Run(r);
            if (res.Count > 1)
            {
                return res;
            }
            else if (res.Count == 0)
            {
                return r;
            }
            else if (res[0].RemainingLength() < r[0].RemainingLength())
            {
                return res;
            }
            else
            {
                return r;
            }
        }

        var result = new List<Result>();

        foreach (var input in r)
        {
            var res = optional.Run([input]);
            if (res.Count > 1)
            {
                result.AddRange(res);
            }
            else if (res.Count == 0)
            {
                result.Add(input);
            }
            else if (res[0].RemainingLength() < input.RemainingLength())
            {
                result.AddRange(res);
            }
            else
            {
                result.Add(input);
            }
        }

        return result;
    }

    public string Railroad() => "Railroad.Optional(" + optional.Railroad() + ")";

    public string ToStr() => "opt(" + optional.ToStr() + ")";

    public List<string> First() => [""];
}

internal sealed class Optional : IStatementRunnable
{
    private readonly IStatementRunnable optional;

    public Optional(IStatementRunnable optional)
    {
        this.optional = optional;
    }

    public List<string> ListKeywords() => optional.ListKeywords();

    public List<string> GetUsing() => optional.GetUsing();

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        if (r.Count == 1)
        {
            // single input, pass and return the input list without wrapping
            var res = optional.Run(r);
            if (res.Count == 0)
            {
                return r;
            }
            var concat = new List<Result>(r.Count + res.Count);
            concat.AddRange(r);
            concat.AddRange(res);
            return concat;
        }

        var result = new List<Result>();

        foreach (var input in r)
        {
            result.Add(input);
            result.AddRange(optional.Run([input]));
        }

        return result;
    }

    public string Railroad() => "Railroad.Optional(" + optional.Railroad() + ")";

    public string ToStr() => "opt(" + optional.ToStr() + ")";

    public List<string> First() => [""];
}

internal sealed class Star : IStatementRunnable
{
    private readonly IStatementRunnable sta;

    public Star(IStatementRunnable sta)
    {
        this.sta = sta;
    }

    public List<string> ListKeywords() => sta.ListKeywords();

    public List<string> GetUsing() => sta.GetUsing();

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        IReadOnlyList<Result> result = r;
        List<Result>? copy = null;

        try
        {
            var res = r;
            for (;;)
            {
                res = sta.Run(res);

                if (res.Count == 0)
                {
                    break;
                }

                if (copy == null)
                {
                    // copy on first match, the input list must not be mutated
                    copy = new List<Result>(r.Count + res.Count);
                    copy.AddRange(r);
                    result = copy;
                }
                copy.AddRange(res);
            }
        }
        catch (FailStarException)
        {
            return result;
        }
        return result;
    }

    public string Railroad() => "Railroad.ZeroOrMore(" + sta.Railroad() + ")";

    public string ToStr() => "star(" + sta.ToStr() + ")";

    public List<string> First() => [""];
}

internal sealed class StarPriority : IStatementRunnable
{
    private readonly IStatementRunnable sta;

    public StarPriority(IStatementRunnable sta)
    {
        this.sta = sta;
    }

    public List<string> ListKeywords() => sta.ListKeywords();

    public List<string> GetUsing() => sta.GetUsing();

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        IReadOnlyList<Result> result = r;

        var res = r;
        IReadOnlyList<Result>? prev = null;
        for (;;)
        {
            res = sta.Run(res);

            if (res.Count == 0)
            {
                if (prev != null)
                {
                    var best = int.MaxValue;
                    foreach (var p in prev)
                    {
                        if (p.RemainingLength() < best)
                        {
                            result = [p];
                            best = p.RemainingLength();
                        }
                    }
                }
                break;
            }

            if (res.Count > 1)
            {
                // Not in abaplint. Each round can hand back several results that stand at the same token; carried into the
                // next round they multiply, and a WHERE of twenty conditions is a million results. This combinator keeps one
                // result in the end, the one that got furthest, so of the results at one position only the first matters.
                var seen = new HashSet<int>();
                var distinct = new List<Result>(res.Count);
                foreach (var one in res)
                {
                    if (seen.Add(one.RemainingLength()))
                    {
                        distinct.Add(one);
                    }
                }
                res = distinct;
            }

            prev = res;
        }

        return result;
    }

    public string Railroad() => "Railroad.ZeroOrMore(" + sta.Railroad() + ")";

    public string ToStr() => "star(" + sta.ToStr() + ")";

    public List<string> First() => [""];
}

internal sealed class PlusCombinator : IStatementRunnable
{
    private readonly IStatementRunnable plu;
    private readonly Sequence sub;

    public PlusCombinator(IStatementRunnable plu)
    {
        this.plu = plu;
        sub = new Sequence([this.plu, new Star(this.plu)]);
    }

    public List<string> ListKeywords() => plu.ListKeywords();

    public List<string> GetUsing() => plu.GetUsing();

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r) => sub.Run(r);

    public string Railroad() => "Railroad.OneOrMore(" + plu.Railroad() + ")";

    public string ToStr() => "plus(" + plu.ToStr() + ")";

    public List<string> First() => plu.First();
}

internal sealed class PlusPriority : IStatementRunnable
{
    private readonly IStatementRunnable plu;
    private readonly Sequence sub;

    public PlusPriority(IStatementRunnable plu)
    {
        this.plu = plu;
        sub = new Sequence([this.plu, new StarPriority(this.plu)]);
    }

    public List<string> ListKeywords() => plu.ListKeywords();

    public List<string> GetUsing() => plu.GetUsing();

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r) => sub.Run(r);

    public string Railroad() => "Railroad.OneOrMore(" + plu.Railroad() + ")";

    public string ToStr() => "plus(" + plu.ToStr() + ")";

    public List<string> First() => plu.First();
}

internal sealed class Sequence : IStatementRunnable
{
    private readonly IStatementRunnable[] list;

    public Sequence(IStatementRunnable[] list)
    {
        if (list.Length < 2)
        {
            throw new ArgumentException("Sequence, length error");
        }
        this.list = list;
    }

    public List<string> ListKeywords()
    {
        var ret = new List<string>();
        foreach (var i in list)
        {
            ret.AddRange(i.ListKeywords());
        }
        return ret;
    }

    public List<string> GetUsing()
    {
        var ret = new List<string>();
        foreach (var i in list)
        {
            ret.AddRange(i.GetUsing());
        }
        return ret;
    }

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        if (r.Count == 1)
        {
            // single input, pass it down without wrapping and return the last result as is
            var temp = r;
            foreach (var sequence in list)
            {
                temp = sequence.Run(temp);
                if (temp.Count == 0)
                {
                    return CombiShared.EMPTY;
                }
            }
            return temp;
        }

        var result = new List<Result>();

        foreach (var input in r)
        {
            IReadOnlyList<Result> temp = [input];
            var match = true;
            foreach (var sequence in list)
            {
                temp = sequence.Run(temp);
                if (temp.Count == 0)
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                result.AddRange(temp);
            }
        }

        return result;
    }

    public string Railroad() => "Railroad.Sequence(" + string.Join(",", list.Select(e => e.Railroad())) + ")";

    public string ToStr()
    {
        var ret = "";
        foreach (var i in list)
        {
            ret = ret + i.ToStr() + ",";
        }
        return "seq(" + ret + ")";
    }

    public List<string> First() => list[0].First();
}

internal sealed class WordSequence : IStatementRunnable
{
    private readonly string stri;
    private readonly List<IStatementRunnable> words = [];
    private readonly Sequence seq;

    public WordSequence(string stri)
    {
        this.stri = stri;

        var foo = this.stri.Replace("-", " - ");
        var split = foo.Split(' ');

        foreach (var st in split)
        {
            // todo, use Dash token
            words.Add(new Word(st));
        }
        seq = new Sequence(words.ToArray());
    }

    public List<string> ListKeywords() => [stri];

    public List<string> GetUsing() => [];

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r) => seq.Run(r);

    public string Railroad() => "Railroad.Terminal('\"" + stri + "\"')";

    public string ToStr() => "str(" + stri + ")";

    public List<string> First() => words[0].First();
}

/// <summary>
/// Base class of all expressions (abap/2_statements/expressions). A subclass implements
/// <see cref="GetRunnable"/>; the result of a match is wrapped in an <see cref="ExpressionNode"/>.
/// The class name is the expression name.
/// </summary>
public abstract class Expression : IStatementRunnable
{
    private IStatementRunnable? runnable;

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        var runnable = this.runnable ??= GetRunnable();

        if (r.Count == 1)
        {
            // single input, pass it down without wrapping
            var input = r[0];
            var temp = runnable.Run(r);
            if (temp.Count == 0)
            {
                return CombiShared.EMPTY;
            }
            var results = new Result[temp.Count];
            for (var i = 0; i < temp.Count; i++)
            {
                var t = temp[i];
                var consumed = input.RemainingLength() - t.RemainingLength();
                results[i] = consumed > 0 ? t.WrapConsumed(consumed, new ExpressionNode(this)) : t;
            }
            return results;
        }

        List<Result>? resultList = null;

        foreach (var input in r)
        {
            var temp = runnable.Run([input]);
            if (temp.Count == 0)
            {
                continue;
            }
            resultList ??= [];

            foreach (var t in temp)
            {
                var consumed = input.RemainingLength() - t.RemainingLength();
                if (consumed > 0)
                {
                    var re = new ExpressionNode(this);
                    resultList.Add(t.WrapConsumed(consumed, re));
                }
                else
                {
                    resultList.Add(t);
                }
            }
        }
        return resultList ?? CombiShared.EMPTY;
    }

    public abstract IStatementRunnable GetRunnable();

    public List<string> ListKeywords()
    {
        // do not recurse, all Expressions are evaluated only on first level
        return [];
    }

    public List<string> GetUsing() => ["expression/" + GetName()];

    public virtual string GetName() => GetType().Name;

    public string Railroad() => "Railroad.NonTerminal('" + GetName() + "', {href: '#/expression/" + GetName() + "'})";

    public string ToStr() => "expression(" + GetName() + ")";

    public List<string> First() => GetRunnable().First();

    public override string ToString() => GetName();
}

internal sealed class Permutation : IStatementRunnable
{
    private readonly IStatementRunnable[] list;

    public Permutation(IStatementRunnable[] list)
    {
        if (list.Length < 2)
        {
            throw new ArgumentException("Permutation, length error, got " + list.Length);
        }
        this.list = list;
    }

    public List<string> ListKeywords()
    {
        var ret = new List<string>();
        foreach (var i in list)
        {
            ret.AddRange(i.ListKeywords());
        }
        return ret;
    }

    public List<string> GetUsing()
    {
        var ret = new List<string>();
        foreach (var i in list)
        {
            ret.AddRange(i.GetUsing());
        }
        return ret;
    }

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        List<Result>? result = null;
        List<IStatementRunnable>? copy = null;

        for (var index = 0; index < list.Length; index++)
        {
            var temp = list[index].Run(r);
            if (temp.Count != 0)
            {
                // match
                if (result == null || copy == null)
                {
                    result = [];
                    copy = list.ToList();
                }
                result.AddRange(temp);

                // faithful to upstream: "copy" is spliced in place, so later iterations see the
                // already shortened list
                var left = copy;
                if (index < left.Count)
                {
                    left.RemoveAt(index);
                }
                if (left.Count == 1)
                {
                    result.AddRange(left[0].Run(temp));
                }
                else
                {
                    result.AddRange(new Permutation(left.ToArray()).Run(temp));
                }
            }
        }
        return result ?? CombiShared.EMPTY;
    }

    public string Railroad() => "Railroad.MultipleChoice(0, 'any'," + string.Join(",", list.Select(e => e.Railroad())) + ")";

    public string ToStr() => "per(" + string.Join(",", list.Select(e => e.ToStr())) + ")";

    public List<string> First() => [""];
}

internal sealed class FailCombinatorException : Exception
{
}

internal sealed class FailStarException : Exception
{
}

internal sealed class FailCombinatorRunnable : IStatementRunnable
{
    public List<string> ListKeywords() => [];

    public List<string> GetUsing() => [];

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r) => throw new FailCombinatorException();

    public string Railroad() => "Railroad.Terminal('!FailCombinator')";

    public string ToStr() => "fail()";

    public List<string> First() => [];
}

// Note that Plus is implemented with Star
internal sealed class FailStarRunnable : IStatementRunnable
{
    public List<string> ListKeywords() => [];

    public List<string> GetUsing() => [];

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r) => throw new FailStarException();

    public string Railroad() => "Railroad.Terminal('!FailStar')";

    public string ToStr() => "fail()";

    public List<string> First() => [];
}

internal sealed class Alternative : IStatementRunnable
{
    private readonly IStatementRunnable[] list;

    public Alternative(IStatementRunnable[] list)
    {
        if (list.Length < 2)
        {
            throw new ArgumentException("Alternative, length error");
        }
        this.list = list;
    }

    public List<string> ListKeywords()
    {
        var ret = new List<string>();
        foreach (var i in list)
        {
            ret.AddRange(i.ListKeywords());
        }
        return ret;
    }

    public List<string> GetUsing()
    {
        var ret = new List<string>();
        foreach (var i in list)
        {
            ret.AddRange(i.GetUsing());
        }
        return ret;
    }

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        // only allocate a new list when more than one alternative matches
        IReadOnlyList<Result>? single = null;
        List<Result>? result = null;

        foreach (var sequ in list)
        {
            var temp = sequ.Run(r);
            if (temp.Count == 0)
            {
                continue;
            }
            else if (result != null)
            {
                result.AddRange(temp);
            }
            else if (single != null)
            {
                result = new List<Result>(single.Count + temp.Count);
                result.AddRange(single);
                result.AddRange(temp);
            }
            else
            {
                single = temp;
            }
        }

        return (IReadOnlyList<Result>?)result ?? single ?? CombiShared.EMPTY;
    }

    public string Railroad() => "Railroad.Choice(0, " + string.Join(",", list.Select(e => e.Railroad())) + ")";

    public string ToStr()
    {
        var ret = "";
        foreach (var i in list)
        {
            ret = ret + i.ToStr() + ",";
        }
        return "alt(" + ret + ")";
    }

    public List<string> First() => Combi.FirstOfTwo(list);
}

// prioritized alternative, skip others if match found
internal sealed class AlternativePriority : IStatementRunnable
{
    private readonly IStatementRunnable[] list;

    public AlternativePriority(IStatementRunnable[] list)
    {
        if (list.Length < 2)
        {
            throw new ArgumentException("Alternative, length error");
        }
        this.list = list;
    }

    public List<string> ListKeywords()
    {
        var ret = new List<string>();
        foreach (var i in list)
        {
            ret.AddRange(i.ListKeywords());
        }
        return ret;
    }

    public List<string> GetUsing()
    {
        var ret = new List<string>();
        foreach (var i in list)
        {
            ret.AddRange(i.GetUsing());
        }
        return ret;
    }

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        foreach (var sequ in list)
        {
            var temp = sequ.Run(r);
            if (temp.Count > 0)
            {
                return temp;
            }
        }

        return CombiShared.EMPTY;
    }

    public string Railroad() => "Railroad.Choice(0, " + string.Join(",", list.Select(e => e.Railroad())) + ")";

    public string ToStr()
    {
        var ret = "";
        foreach (var i in list)
        {
            ret = ret + i.ToStr() + ",";
        }
        return "alt(" + ret + ")";
    }

    public List<string> First() => Combi.FirstOfTwo(list);
}

// Passes through results where the next two tokens do NOT match t1 followed by t2.
internal sealed class StopBefore2 : IStatementRunnable
{
    private readonly string t1;
    private readonly string t2;

    public StopBefore2(string t1, string t2)
    {
        this.t1 = JsString.ToUpper(t1);
        this.t2 = JsString.ToUpper(t2);
    }

    public List<string> ListKeywords() => [];

    public List<string> GetUsing() => [];

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        // only copy when something is filtered out
        List<Result>? result = null;
        for (var i = 0; i < r.Count; i++)
        {
            var input = r[i];
            var next = input.PeekOrNull();
            if (next == null
                || (next.GetUpperStr() == t1 && input.PeekAt(1)?.GetUpperStr() == t2))
            {
                result ??= r.Take(i).ToList();
                continue;
            }
            result?.Add(input);
        }
        return (IReadOnlyList<Result>?)result ?? r;
    }

    public string Railroad() => "Railroad.Terminal('stopBefore(" + t1 + " " + t2 + ")')";

    public string ToStr() => "stopBefore(" + t1 + "," + t2 + ")";

    public List<string> First() => [""];
}

internal sealed class StopBefore1Runnable : IStatementRunnable
{
    private readonly string[] words;

    public StopBefore1Runnable(string[] words)
    {
        this.words = words.Select(JsString.ToUpper).ToArray();
    }

    public List<string> ListKeywords() => [];

    public List<string> GetUsing() => [];

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r)
    {
        // only copy when something is filtered out
        List<Result>? result = null;
        for (var i = 0; i < r.Count; i++)
        {
            var input = r[i];
            var next = input.PeekOrNull()?.GetUpperStr();
            if (next != null && words.Contains(next))
            {
                result ??= r.Take(i).ToList();
                continue;
            }
            result?.Add(input);
        }
        return (IReadOnlyList<Result>?)result ?? r;
    }

    public string Railroad() => "Railroad.Terminal('stopBefore(" + string.Join("|", words) + ")')";

    public string ToStr() => "stopBefore(" + string.Join(",", words) + ")";

    public List<string> First() => [""];
}

/// <summary>
/// The TS class <c>Combi</c> (static parser state, <c>Run</c>) together with the TS module level
/// factory functions (<c>seq</c>, <c>alt</c>, ...), PascalCased. Import with
/// <c>using static ScipSharp.Indexer.Abap.Statements.Combi;</c>.
/// </summary>
public static class Combi
{
    // todo, change this class to be instantiated, constructor(runnable) ?
    // the state is per thread, so parsers on different threads do not interfere
    [ThreadStatic] private static ABAPRelease? release;
    [ThreadStatic] private static LanguageVersion langVer;

    public static string Railroad(IStatementRunnable runnable, bool complex = false)
    {
        // todo, move method to graph.js?
        var type = "Railroad.Diagram(";
        if (complex)
        {
            type = "Railroad.ComplexDiagram(";
        }

        return "Railroad.Diagram.INTERNAL_ALIGNMENT = 'left';\n" + type + runnable.Railroad() + ").toString();";
    }

    public static List<string> ListKeywords(IStatementRunnable runnable)
    {
        // remove duplicates
        return runnable.ListKeywords().Distinct().ToList();
    }

    /// <summary>Assumption: no pragmas supplied in tokens input.</summary>
    public static List<IExpressionOrTokenNode>? Run(
        IStatementRunnable runnable,
        IReadOnlyList<AbstractToken> tokens,
        ABAPRelease releaseIn,
        LanguageVersion languageVersion = LanguageVersion.Normal)
    {
        langVer = languageVersion;
        release = releaseIn;

        var input = new Result(tokens, 0);
        try
        {
            var result = runnable.Run([input]);
            foreach (var res in result)
            {
                if (res.RemainingLength() == 0)
                {
                    return res.GetNodes();
                }
            }
        }
        catch (FailCombinatorException)
        {
            return null;
        }

        return null;
    }

    public static List<IExpressionOrTokenNode>? Run(
        IStatementRunnable runnable,
        IReadOnlyList<AbstractToken> tokens,
        Version version,
        LanguageVersion languageVersion = LanguageVersion.Normal)
    {
        return Run(runnable, tokens, Versions.VersionToABAPRelease(version), languageVersion);
    }

    public static ABAPRelease GetRelease() => release ?? Release.v758;

    public static LanguageVersion GetLanguageVersion() => langVer;

    public static bool IsOpenABAP() => GetRelease() == Release.OpenABAP;

    internal static int LangVersionOrdinal(LanguageVersion v) => v switch
    {
        LanguageVersion.KeyUser => 0,
        LanguageVersion.Cloud => 1,
        LanguageVersion.Normal => 2,
        _ => 2,
    };

    internal static List<string> FirstOfTwo(IStatementRunnable[] list)
    {
        if (list.Length != 2)
        {
            return [""];
        }
        var f1 = list[0].First();
        var f2 = list[1].First();
        if (f1.Count == 1 && f1[0] == "")
        {
            return f1;
        }
        if (f2.Count == 1 && f2[0] == "")
        {
            return f2;
        }
        if (f1.Count == 1 && f2.Count == 1 && f1[0] == f2[0])
        {
            return f1;
        }
        f1.AddRange(f2);
        return f1;
    }

    // -----------------------------------------------------------------------------
    // factory functions

    private static readonly ConcurrentDictionary<Type, Expression> expressionSingletons = new();
    private static readonly ConcurrentDictionary<string, IStatementRunnable> stringSingletons = new();
    private static readonly ConcurrentDictionary<(string, SysRegexOptions), SysRegex> regexCache = new();

    /// <summary>TS <c>str("A B")</c>: a keyword, or a sequence of keywords when it contains spaces or dashes.</summary>
    public static IStatementRunnable Str(string s)
    {
        if (s.IndexOf(' ') > 0 || s.IndexOf('-') > 0)
        {
            return new WordSequence(s);
        }
        return new Word(s);
    }

    /// <summary>
    /// TS <c>regex(/pattern/flags)</c>, e.g. <c>Regex(@"^\d+$")</c> or <c>Regex(@"^BOOLC$", "i")</c>.
    /// The pattern is compiled with ECMAScript semantics (\w and \d are ASCII only, as in JS).
    /// </summary>
    public static IStatementRunnable Regex(string pattern, string flags = "")
    {
        var options = SysRegexOptions.CultureInvariant;
        if (flags.Contains('i')) options |= SysRegexOptions.IgnoreCase;
        if (flags.Contains('m')) options |= SysRegexOptions.Multiline;
        var regex = regexCache.GetOrAdd((pattern, options), key =>
        {
            try
            {
                return new SysRegex(key.Item1, key.Item2 | SysRegexOptions.ECMAScript);
            }
            catch (ArgumentException)
            {
                // constructs not supported in ECMAScript mode, fall back to .NET semantics
                return new SysRegex(key.Item1, key.Item2);
            }
        });
        return new RegexCombinator(regex, pattern, flags);
    }

    /// <summary>TS <c>tok(ParenLeftW)</c>: matches exactly one token of the given class.</summary>
    public static IStatementRunnable Tok<T>() where T : AbstractToken => new TokenCombinator(typeof(T));

    /// <summary>TS <c>tok(t)</c> with the token class given as a <see cref="System.Type"/>.</summary>
    public static IStatementRunnable Tok(Type t) => new TokenCombinator(t);

    /// <summary>The shared singleton instance of an expression, the C# counterpart of naming the TS class.</summary>
    public static T Expr<T>() where T : Expression, new() => (T)expressionSingletons.GetOrAdd(typeof(T), _ => new T());

    /// <summary>
    /// Maps a combinator input: string → keyword (singleton per string), IStatementRunnable → itself,
    /// Type of an Expression subclass → its singleton.
    /// </summary>
    public static IStatementRunnable MapInput(object s)
    {
        switch (s)
        {
            case string str:
                return stringSingletons.GetOrAdd(str, Str);
            case IStatementRunnable runnable:
                return runnable;
            case Type type when typeof(Expression).IsAssignableFrom(type):
                return expressionSingletons.GetOrAdd(type, t => (Expression)Activator.CreateInstance(t)!);
            case Type type when typeof(AbstractToken).IsAssignableFrom(type):
                throw new ArgumentException("Combi input: token types must be wrapped in Tok<" + type.Name + ">()");
            case null:
                throw new ArgumentNullException(nameof(s), "Combi input is null, check for an expression referencing a static field before it is initialized");
            default:
                throw new ArgumentException("Combi input: unsupported type " + s.GetType().Name);
        }
    }

    private static IStatementRunnable[] MapList(object first, object second, object[] rest)
    {
        var list = new IStatementRunnable[rest.Length + 2];
        list[0] = MapInput(first);
        list[1] = MapInput(second);
        for (var i = 0; i < rest.Length; i++)
        {
            list[i + 2] = MapInput(rest[i]);
        }
        return list;
    }

    private static IStatementRunnable[] MapList(object[] items) => items.Select(MapInput).ToArray();

    public static IStatementRunnable Seq(object first, object second, params object[] rest) => new Sequence(MapList(first, second, rest));

    /// <summary>For TS spread calls, <c>seq(a, ...list)</c>: pass all items as one array, at least two.</summary>
    public static IStatementRunnable SeqOf(params object[] items) => new Sequence(MapList(items));

    public static IStatementRunnable Alt(object first, object second, params object[] rest) => new Alternative(MapList(first, second, rest));

    public static IStatementRunnable AltOf(params object[] items) => new Alternative(MapList(items));

    public static IStatementRunnable AltPrio(object first, object second, params object[] rest) => new AlternativePriority(MapList(first, second, rest));

    public static IStatementRunnable AltPrioOf(params object[] items) => new AlternativePriority(MapList(items));

    public static IStatementRunnable Opt(object first) => new Optional(MapInput(first));

    public static IStatementRunnable OptPrio(object first) => new OptionalPriority(MapInput(first));

    public static IStatementRunnable Per(object first, object second, params object[] rest) => new Permutation(MapList(first, second, rest));

    public static IStatementRunnable PerOf(params object[] items) => new Permutation(MapList(items));

    public static IStatementRunnable Star(object first) => new Star(MapInput(first));

    public static IStatementRunnable StarPrio(object first) => new StarPriority(MapInput(first));

    public static IStatementRunnable Plus(object first) => new PlusCombinator(MapInput(first));

    public static IStatementRunnable PlusPrio(object first) => new PlusPriority(MapInput(first));

    /// <summary>TS <c>ver(Release.v740sp02, x, {also: AlsoIn.OpenABAP})</c> → <c>Ver(Release.v740sp02, x, also: AlsoIn.OpenABAP)</c>.</summary>
    public static IStatementRunnable Ver(ABAPRelease release, object first, AlsoIn? also = null) => new Vers(release, MapInput(first), also);

    public static IStatementRunnable Ver(Version version, object first, AlsoIn? also = null) => new Vers(Versions.VersionToABAPRelease(version), MapInput(first), also);

    public static IStatementRunnable VerLang(LanguageVersion langVersion, object first) => new LangVers(langVersion, MapInput(first));

    public static IStatementRunnable VerNotLang(LanguageVersion langVersion, object first) => new LangVersNot(langVersion, MapInput(first));

    public static IStatementRunnable FailCombinator() => new FailCombinatorRunnable();

    public static IStatementRunnable FailStar() => new FailStarRunnable();

    public static IStatementRunnable StopBefore(string t1, string t2) => new StopBefore2(t1, t2);

    public static IStatementRunnable StopBefore1(params string[] words) => new StopBefore1Runnable(words);

    /// <summary>
    /// Placeholder for not yet ported expressions/statements: never matches anything.
    /// Returns a runnable with an empty "first" keyword so a statement using it is put in the
    /// keyword-less bucket of the statement map.
    /// </summary>
    public static IStatementRunnable NotPorted(string what) => new NotPortedRunnable(what);
}

internal sealed class NotPortedRunnable(string what) : IStatementRunnable
{
    public List<string> ListKeywords() => [];

    public List<string> GetUsing() => [];

    public IReadOnlyList<Result> Run(IReadOnlyList<Result> r) => CombiShared.EMPTY;

    public string Railroad() => "Railroad.Terminal('!NotPorted " + what + "')";

    public string ToStr() => "notPorted(" + what + ")";

    public List<string> First() => [""];
}
