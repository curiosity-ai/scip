// Ported from abaplint packages/core/src/abap/3_structures/structure_parser.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using System.Collections.Concurrent;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Structures.Structures;
using ScipSharp.Indexer.Abap.Files;


namespace ScipSharp.Indexer.Abap.Structures;

using SysRegex = System.Text.RegularExpressions.Regex;
using SysRegexOptions = System.Text.RegularExpressions.RegexOptions;
using Type = System.Type;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public static class StructureParser
{
    private static readonly ConcurrentDictionary<Type, IStructureRunnable> singletons = new();

    private static readonly SysRegex screenRegex =
        new(@"\.screen_\d+\.abap$", SysRegexOptions.IgnoreCase | SysRegexOptions.ECMAScript | SysRegexOptions.Compiled);

    public static IStructureResult Run(IStatementResult input)
    {
        var structure = FindStructureForFile(input.File.GetFilename());

        var filtered = new List<StatementNode>();
        foreach (var s in input.Statements)
        {
            var get = s.Get();
            if (get is Statements.Comment || get is Statements.Empty || get is Statements.Unknown)
            {
                continue;
            }
            filtered.Add(s);
        }

        return RunFile(structure, input.File, filtered);
    }

    //////////////////

    /// <summary>The root structure the statements of a file are matched against, picked by file name.</summary>
    public static IStructure FindStructureForFile(string filename)
    {
        // todo, not sure this is the right place for this logic
        if (filename.EndsWith(".clas.abap", StringComparison.Ordinal))
        {
            return new ClassGlobal();
        }
        else if (filename.EndsWith(".intf.abap", StringComparison.Ordinal))
        {
            return new InterfaceGlobal();
        }
        else if (screenRegex.IsMatch(filename))
        {
            return new DynproLogic();
        }
        else
        {
            // todo, add a special structure for TYPE-POOLS
            return new Any();
        }
    }

    private static IStructureResult RunFile(IStructure structure, IFile file, List<StatementNode> statements)
    {
        var parent = new StructureNode(structure);
        var matcher = singletons.GetOrAdd(structure.GetType(), _ => structure.GetMatcher());
        IMatch result;
        try
        {
            result = matcher.Run(statements, parent);
        }
        catch (InsufficientExecutionStackException)
        {
            // see SubStructure.RunAt
            var issue = Issue.AtPosition(file, new Position(1, 1), "Structure nested too deeply to parse", "structure", Severity.Error);
            return new IStructureResult([issue], null);
        }

        if (result.Error)
        {
            var issue = Issue.AtPosition(file, new Position(1, 1), result.ErrorDescription, "structure", Severity.Error);
            return new IStructureResult([issue], null);
        }
        if (result.Unmatched.Count > 0)
        {
            var statement = result.Unmatched[0];
            var descr = "Unexpected " + JsString.ToUpper(statement.Get().GetType().Name);
            var issue = Issue.AtPosition(file, statement.GetStart(), descr, "structure", Severity.Error);
            return new IStructureResult([issue], null);
        }

        return new IStructureResult([], parent);
    }
}
