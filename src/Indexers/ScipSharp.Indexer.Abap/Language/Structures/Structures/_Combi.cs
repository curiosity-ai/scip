// Ported from abaplint packages/core/src/abap/3_structures/structures/_combi.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
//
// The structure combinators. Structure definitions use them through
// "using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;":
//   seq(sta(Statements.If), opt(sub(Body)), sta(Statements.EndIf))          (TS)
//   Seq(Sta<Statements.If>(), Opt(Sub<Body>()), Sta<Statements.EndIf>())    (C#)
using System.Collections.Concurrent;
using ScipSharp.Indexer.Abap.Nodes;


namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Type = System.Type;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

internal static class StructureMatch
{
    public static IMatch ToMatch(List<StatementNode> statements, IMatchAt m)
    {
        var next = Math.Min(m.Next, statements.Count);
        return new IMatch
        {
            Matched = statements.GetRange(0, next),
            Unmatched = statements.GetRange(next, statements.Count - next),
            Error = m.Error,
            ErrorDescription = m.ErrorDescription,
            ErrorMatched = m.ErrorMatched,
        };
    }

    public static IMatchAt Success(int next) =>
        new() { Next = next, Error = false, ErrorDescription = "", ErrorMatched = 0 };

    public static IMatchAt Failure(int next, string errorDescription, int errorMatched) =>
        new() { Next = next, Error = true, ErrorDescription = errorDescription, ErrorMatched = errorMatched };
}

internal sealed class Sequence : IStructureRunnable
{
    private readonly IStructureRunnable[] list;

    public Sequence(IStructureRunnable[] list)
    {
        if (list.Length < 2)
        {
            throw new ArgumentException("Sequence, length error");
        }
        this.list = list;
    }

    public string ToRailroad()
    {
        var children = list.Select(e => e.ToRailroad());
        return "Railroad.Sequence(" + string.Join(",", children) + ")";
    }

    public List<string> GetUsing() => list.SelectMany(c => c.GetUsing()).ToList();

    public List<string> First() => list[0].First();

    public IMatch Run(List<StatementNode> statements, INode parent) =>
        StructureMatch.ToMatch(statements, RunAt(statements, 0, parent));

    public IMatchAt RunAt(List<StatementNode> statements, int index, INode parent)
    {
        var next = index;
        foreach (var i in list)
        {
            var match = i.RunAt(statements, next, parent);
            if (match.Error)
            {
                return StructureMatch.Failure(index, match.ErrorDescription, next - index);
            }
            next = match.Next;
        }
        return StructureMatch.Success(next);
    }
}

/// <summary>Note that the Alternative does not nessesarily return the first in the alternative
/// as a map is used for better performance.</summary>
internal sealed class Alternative : IStructureRunnable
{
    private readonly IStructureRunnable[] list;
    // dont build from the constructor, it will cause infinite loop
    private readonly Lazy<Dictionary<string, List<IStructureRunnable>>> map;

    public Alternative(IStructureRunnable[] list)
    {
        if (list.Length < 2)
        {
            throw new ArgumentException("Alternative, length error");
        }
        this.list = list;
        map = new Lazy<Dictionary<string, List<IStructureRunnable>>>(SetupMap, LazyThreadSafetyMode.PublicationOnly);
    }

    private Dictionary<string, List<IStructureRunnable>> SetupMap()
    {
        var ret = new Dictionary<string, List<IStructureRunnable>>();
        foreach (var i in list)
        {
            foreach (var first in i.First())
            {
                if (ret.TryGetValue(first, out var l))
                {
                    l.Add(i);
                }
                else
                {
                    ret[first] = [i];
                }
            }
        }
        return ret;
    }

    public List<string> First() => [""];

    public string ToRailroad()
    {
        var children = list.Select(e => e.ToRailroad());
        return "Railroad.Choice(0, " + string.Join(",", children) + ")";
    }

    public List<string> GetUsing() => list.SelectMany(c => c.GetUsing()).ToList();

    public IMatch Run(List<StatementNode> statements, INode parent) =>
        StructureMatch.ToMatch(statements, RunAt(statements, 0, parent));

    public IMatchAt RunAt(List<StatementNode> statements, int index, INode parent)
    {
        var m = map.Value;
        var count = 0;
        var countError = "";

        if (index >= statements.Count)
        {
            return StructureMatch.Failure(index, countError, count);
        }

        var token = JsString.ToUpper(statements[index].GetFirstToken().GetStr());
        if (m.TryGetValue(token, out var candidates))
        {
            foreach (var i in candidates)
            {
                var match = i.RunAt(statements, index, parent);
                if (match.Error == false)
                {
                    return match;
                }
                if (match.ErrorMatched > count)
                {
                    countError = match.ErrorDescription;
                    count = match.ErrorMatched;
                }
            }
        }

        if (m.TryGetValue("", out var rest))
        {
            foreach (var i in rest)
            {
                var match = i.RunAt(statements, index, parent);
                if (match.Error == false)
                {
                    return match;
                }
                if (match.ErrorMatched > count)
                {
                    countError = match.ErrorDescription;
                    count = match.ErrorMatched;
                }
            }
        }

        if (count == 0)
        {
            return StructureMatch.Failure(index, "Unexpected code structure", count);
        }
        else
        {
            return StructureMatch.Failure(index, countError, count);
        }
    }
}

internal sealed class Optional : IStructureRunnable
{
    private readonly IStructureRunnable obj;

    public Optional(IStructureRunnable obj)
    {
        this.obj = obj;
    }

    public string ToRailroad() => "Railroad.Optional(" + obj.ToRailroad() + ")";

    public List<string> GetUsing() => obj.GetUsing();

    public IMatch Run(List<StatementNode> statements, INode parent) =>
        StructureMatch.ToMatch(statements, RunAt(statements, 0, parent));

    public IMatchAt RunAt(List<StatementNode> statements, int index, INode parent)
    {
        var ret = obj.RunAt(statements, index, parent);
        ret.Error = false;
        return ret;
    }

    public List<string> First() => [""];
}

internal sealed class Star : IStructureRunnable
{
    private readonly IStructureRunnable obj;

    public Star(IStructureRunnable obj)
    {
        this.obj = obj;
    }

    public string ToRailroad() => "Railroad.ZeroOrMore(" + obj.ToRailroad() + ")";

    public List<string> GetUsing() => obj.GetUsing();

    public IMatch Run(List<StatementNode> statements, INode parent) =>
        StructureMatch.ToMatch(statements, RunAt(statements, 0, parent));

    public IMatchAt RunAt(List<StatementNode> statements, int index, INode parent)
    {
        var next = index;
        while (true)
        {
            if (next >= statements.Count)
            {
                return StructureMatch.Success(next);
            }

            var match = obj.RunAt(statements, next, parent);

            if (match.Error == true)
            {
                if (match.ErrorMatched > 0)
                {
                    return StructureMatch.Failure(next, match.ErrorDescription, match.ErrorMatched);
                }
                else
                {
                    return StructureMatch.Success(next);
                }
            }

            next = match.Next;
        }
    }

    public List<string> First() => [""];
}

internal sealed class SubStructure : IStructureRunnable
{
    private readonly IStructure s;
    // SubStructures are singletons, so the getMatcher can be saved, its expensive to create
    // dont build it from the constructor, as it might trigger infinite recursion
    private readonly Lazy<IStructureRunnable> matcher;

    public SubStructure(IStructure s)
    {
        this.s = s;
        matcher = new Lazy<IStructureRunnable>(s.GetMatcher, LazyThreadSafetyMode.PublicationOnly);
    }

    public string ToRailroad()
    {
        var name = s.GetType().Name;
        return "Railroad.NonTerminal('" + name + "', {href: '#/structure/" + name + "'})";
    }

    public List<string> GetUsing() => ["structure/" + s.GetType().Name];

    public List<string> First() => matcher.Value.First();

    public IMatch Run(List<StatementNode> statements, INode parent) =>
        StructureMatch.ToMatch(statements, RunAt(statements, 0, parent));

    public IMatchAt RunAt(List<StatementNode> statements, int index, INode parent)
    {
        // Not in abaplint: structures nest (IF in IF in ...) and are matched recursively, so deep nesting could run
        // out of stack, which kills the process. Give up instead, StructureParser reports the file as not parsed.
        if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new InsufficientExecutionStackException();
        }
        var nparent = new StructureNode(s);
        var ret = matcher.Value.RunAt(statements, index, nparent);
        if (ret.Next == index)
        {
            ret.Error = true;
        }
        else
        {
            parent.AddChild(nparent);
        }
        return ret;
    }
}

internal sealed class SubStatement : IStructureRunnable
{
    private readonly Type obj;

    public SubStatement(Type obj)
    {
        if (!typeof(Statements.IStatement).IsAssignableFrom(obj))
        {
            throw new ArgumentException("SubStatement, not a statement: " + obj.FullName);
        }
        this.obj = obj;
    }

    public List<string> First()
    {
        var o = (Statements.IStatement)Activator.CreateInstance(obj)!;
        if (o is Statements.MacroCall || o is Statements.MacroRecursion || o is Statements.NativeSQL)
        {
            return [""];
        }
        return o.GetMatcher().First();
    }

    public string ToRailroad() =>
        "Railroad.Terminal('" + ClassName() + "', {href: '#/statement/" + ClassName() + "'})";

    public List<string> GetUsing() => ["statement/" + ClassName()];

    private string ClassName() => obj.Name;

    public IMatch Run(List<StatementNode> statements, INode parent) =>
        StructureMatch.ToMatch(statements, RunAt(statements, 0, parent));

    public IMatchAt RunAt(List<StatementNode> statements, int index, INode parent)
    {
        if (index >= statements.Count)
        {
            return StructureMatch.Failure(statements.Count, "Expected " + JsString.ToUpper(ClassName()), 0);
        }
        else if (obj.IsInstanceOfType(statements[index].Get()))
        {
            parent.AddChild(statements[index]);
            return StructureMatch.Success(index + 1);
        }
        else
        {
            return StructureMatch.Failure(index, "Expected " + JsString.ToUpper(ClassName()), 0);
        }
    }
}

public static class Combi
{
    public static IStructureRunnable Seq(IStructureRunnable first, params IStructureRunnable[] rest) =>
        new Sequence([first, .. rest]);

    public static IStructureRunnable Alt(IStructureRunnable first, params IStructureRunnable[] rest) =>
        new Alternative([first, .. rest]);

    public static IStructureRunnable BeginEnd(IStructureRunnable begin, IStructureRunnable body, IStructureRunnable end) =>
        new Sequence([begin, body, end]);

    public static IStructureRunnable Opt(IStructureRunnable o) => new Optional(o);

    public static IStructureRunnable Star(IStructureRunnable s) => new Structures.Star(s);

    public static IStructureRunnable Sta<T>() where T : Statements.IStatement, new() => new SubStatement(typeof(T));

    /// <summary>TS sta(s) with the statement class given as a System.Type.</summary>
    public static IStructureRunnable Sta(Type s) => new SubStatement(s);

    private static readonly ConcurrentDictionary<Type, SubStructure> singletons = new();

    public static IStructureRunnable Sub<T>() where T : IStructure, new() => Sub(typeof(T));

    /// <summary>TS sub(s) with the structure class given as a System.Type.</summary>
    public static IStructureRunnable Sub(Type s)
    {
        return singletons.GetOrAdd(s, t =>
        {
            if (!typeof(IStructure).IsAssignableFrom(t))
            {
                throw new ArgumentException("Sub, not a structure: " + t.FullName);
            }
            return new SubStructure((IStructure)Activator.CreateInstance(t)!);
        });
    }
}
