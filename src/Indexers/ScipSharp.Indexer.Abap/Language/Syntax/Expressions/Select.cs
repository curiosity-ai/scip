// Ported from abaplint packages/core/src/abap/5_syntax/expressions/select.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Objects;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

// type FieldList = {code: string, as: string, expression: ExpressionNode}[]; (mapped at use sites)

public class DatabaseSourceInfo
{
    public IParseTypeObject? Source { get; set; }
    public string Name { get; set; }
    public string Alias { get; set; }
}

public class Select
{
    internal static readonly Regex IsSimple = new Regex(@"^\w+$", RegexOptions.ECMAScript);
    public static void RunSyntax(ExpressionNode node, SyntaxInput input, bool skipImplicitInto = false, bool selectLoop = false)
    {
        var token = node.GetFirstToken();

        ExpressionNode? from = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLFrom>();
        if (from == null)
        {
            // huh, sometimes the select expression is wrapped
            from = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLFrom>();
        }
        List<IParseTypeObject?> dbSources = from != null ? SQLFrom.RunSyntax(from, input) : new List<IParseTypeObject?>();
        if (from == null)
        {
            var message = "Missing FROM";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var fields = Select.FindFields(node, input);
        if (fields.Count == 0 && node.FindDirectExpression<SQLFieldListLoop>() == null && node.FindDirectExpression<SQLAggregation>() == null)
        {
            var message = "SELECT: fields missing";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        CheckDatabaseFieldsHelper.CheckDatabaseFields(fields.Select(field => field.code).ToList(), dbSources, input, node.GetFirstToken());

        var intoExpression = Select.HandleInto(node, input, fields, dbSources, from);

        var fae = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLForAllEntries>();
        if (fae != null)
        {
            input.Scope.Push(ScopeType.OpenSQL, "SELECT", token.GetStart(), input.Filename);
            SQLForAllEntries.RunSyntax(fae, input);
        }

        foreach (var t in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            Target.RunSyntax(t, input);
        }

        // check implicit into, the target field is implict equal to the table name
        if (skipImplicitInto == false && node.FindDirectExpression<SQLIntoTable>() == null && node.FindDirectExpression<SQLIntoList>() == null && node.FindDirectExpression<SQLIntoStructure>() == null)
        {
            var fields2 = node.FindFirstExpression<SQLAggregation>()?.ConcatTokens();
            var c = new Regex(@"^count\(\s*\*\s*\)$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase);
            if (fields2 == null || c.IsMatch(fields2) == false)
            {
                ExpressionNode? body = from?.FindDirectExpression<SQLFromBody>();
                while (body != null)
                {
                    var inner = body.FindDirectExpression<SQLFromBody>();
                    if (inner == null)
                    {
                        break;
                    }
                    body = inner;
                }
                var nameToken = body?.FindDirectExpression<SQLFromSource>();
                if (nameToken != null)
                {
                    var found = input.Scope.FindVariable(nameToken.ConcatTokens());
                    if (found != null)
                    {
                        input.Scope.AddReference(nameToken.GetFirstToken(), found, ReferenceType.DataWriteReference, input.Filename);
                    }
                    else
                    {
                        var message = $"Target variable {nameToken.ConcatTokens()} not found in scope";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                        return;
                    }
                }
            }
        }

        // OFFSET
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSource>())
        {
            SQLSource.RunSyntax(s, input);
        }
        var upToWhere = node.FindDirectExpression<SQLCond>();
        var isCloud = input.Scope.GetLanguageVersion() == LanguageVersion.Cloud;
        foreach (var up in node.FindDirectExpressions<SQLUpTo>())
        {
            // in classic Open SQL (INTO before WHERE) "UP TO" is allowed before INTO,
            // in strict Open SQL (INTO after WHERE) "UP TO" must be placed after INTO
            var intoAfterWhere = upToWhere != null && intoExpression != null && intoExpression.GetFirstToken().GetStart().IsAfter(upToWhere.GetFirstToken().GetStart());
            if (intoExpression != null && Select.IsStrictMode(node, input) && (isCloud || (Select.UpToUsesHostVariable(up) && intoAfterWhere)) && (selectLoop == false || isCloud) && from.GetFirstToken().GetStart().IsBefore(up.GetFirstToken().GetStart()) && up.GetFirstToken().GetStart().IsBefore(intoExpression.GetFirstToken().GetStart()))
            {
                var message = "The addition \"UP TO n ROWS\" must only be placed after the INTO/APPENDING clause.";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, up.GetFirstToken(), message, Select.BuildUpToRowsFix(node, input, up, intoExpression)));
            }
            foreach (var s in up.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSource>())
            {
                SQLSource.RunSyntax(s, input);
            }
        }
        foreach (var fae2 in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLForAllEntries>())
        {
            foreach (var s in fae2.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSource>())
            {
                SQLSource.RunSyntax(s, input);
            }
        }

        foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLCompare>())
        {
            SQLCompare.RunSyntax(s, input, dbSources);
        }

        var orderBy = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLOrderBy>();
        if (orderBy != null)
        {
            SQLOrderBy.RunSyntax(orderBy, input);

            var where = node.FindDirectExpression<SQLCond>();
            if (intoExpression != null && where != null && intoExpression.GetFirstToken().GetStart().IsBefore(orderBy.GetFirstToken().GetStart()) && where.GetFirstToken().GetStart().IsBefore(orderBy.GetFirstToken().GetStart()) && where.GetFirstToken().GetStart().IsBefore(intoExpression.GetFirstToken().GetStart()))
            {
                var message = "ORDER BY must be before INTO, after WHERE";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, orderBy.GetFirstToken(), message));
            }
        }

        if (Select.IsStrictMode(node, input))
        {
            Select.StrictModeChecks(node, input);
        }

        if (input.Scope.GetType() == ScopeType.OpenSQL)
        {
            input.Scope.Pop(node.GetLastToken().GetEnd());
        }
    }

    private static bool UpToUsesHostVariable(ExpressionNode up)
    {
        return up.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSource>()?.GetFirstToken().GetStr() == "@";
    }

    private static IEdit BuildUpToRowsFix(ExpressionNode node, SyntaxInput input, ExpressionNode up, ExpressionNode intoExpression)
    {
        var tokens = node.GetTokens();
        var upLast = up.GetLastToken();
        var next = tokens.ElementAtOrDefault(JsUtils.FindIndex(tokens, t => t == upLast) + 1);
        var deleteEnd = next?.GetStart() ?? upLast.GetEnd();
        var range = new IRange(up.GetFirstToken().GetStart(), deleteEnd);

        return new IEdit { [input.Filename] = new List<ITextEdit> { new ITextEdit(new IRange(intoExpression.GetLastToken().GetEnd(), intoExpression.GetLastToken().GetEnd()), " " + up.ConcatTokens()), new ITextEdit(range, "") } };
    }

    // there are multiple rules, but gotta start somewhere
    private static bool IsStrictMode(ExpressionNode node, SyntaxInput input)
    {
        // strict OpenSQL is always required in the cloud language version
        if (input.Scope.GetLanguageVersion() == LanguageVersion.Cloud)
        {
            return true;
        }

        var into = node.FindDirectExpressionsMulti(typeof(SQLIntoList), typeof(SQLIntoStructure), typeof(SQLIntoTable)).ElementAtOrDefault(0);
        var where = node.FindDirectExpression<SQLCond>();

        // INTO is after WHERE
        if (into != null && where != null && into.GetFirstToken().GetStart().IsAfter(where.GetFirstToken().GetStart()))
        {
            return true;
        }

        // FIELDS is used
        if ((node.FindFirstExpression<SQLFields>()) != null)
        {
            return true;
        }

        // any field is escaped with @
        foreach (var source in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSource>())
        {
            if (source.GetFirstToken().GetStr() == "@")
            {
                return true;
            }
        }

        // comma used in FROM
        var fieldList = node.FindFirstExpression<SQLFieldList>();
        if (fieldList != null && fieldList.FindDirectTokenByText(",") != null)
        {
            return true;
        }

        return false;
    }

    private static void StrictModeChecks(ExpressionNode node, SyntaxInput input)
    {

        var sources = node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSource>();
        foreach (var source in sources)
        {
            var first = source.GetFirstChild();
            if ((first?.Get()) is SQLAliasField)
            {
                continue;
            }
            else if (first?.GetFirstToken().GetStr() == "@")
            {
                continue;
            }
            else if ((first?.GetChildren().ElementAtOrDefault(0).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Constant)
            {
                continue;
            }
            var message = $"SELECT: \"{source.ConcatTokens()}\" must be escaped with @ in strict mode";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
        }
    }

    private static ExpressionNode? HandleInto(ExpressionNode node, SyntaxInput input, List<(string code, string @as, ExpressionNode expression)> fields, List<IParseTypeObject?> dbSources, ExpressionNode from)
    {
        var intoTable = node.FindDirectExpression<SQLIntoTable>();
        if (intoTable != null)
        {
            var inline = intoTable.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null && intoTable.FindDirectTokenByText("INTO") != null && intoTable.FindDirectTokenByText("CORRESPONDING") != null && !(Versions.ReleaseAtLeast(input.Scope.GetRelease(), Release.v755)))
            {
                var message = "INTO CORRESPONDING FIELDS OF TABLE with an inline declaration requires 7.55";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            }
            if (inline != null)
            {
                InlineData.RunSyntax(inline, input, Select.BuildTableType(fields, dbSources, input.Scope, from));
            }
            return intoTable;
        }

        var intoStructure = node.FindDirectExpression<SQLIntoStructure>();
        if (intoStructure != null)
        {
            var inlineList = intoStructure.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inlineList.Count == 1)
            {
                InlineData.RunSyntax(inlineList.ElementAtOrDefault(0), input, Select.BuildStructureType(fields, dbSources, input.Scope));
            }
            else
            {
                foreach (var inline in inlineList)
                {
                    // todo, for now these are voided
                    InlineData.RunSyntax(inline, input, VoidType.Get("SELECT_todo1"));
                }
            }
            return intoStructure;
        }

        var intoList = node.FindDirectExpression<SQLIntoList>();
        if (intoList != null)
        {
            var isDynamic = fields.Count == 1 && fields.ElementAtOrDefault(0).expression.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>() != null;
            var targets = intoList.FindDirectExpressions<SQLTarget>();
            if (targets.Count != fields.Count && isDynamic != true)
            {
                var message = "number of fields selected vs list does not match";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return null;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var field = fields.ElementAtOrDefault(i);

                var inline = target.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
                if (inline != null)
                {
                    if (isDynamic)
                    {
                        var message = "dynamic field list, inlining not possible";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                        return null;
                    }

                    AbstractType type = VoidType.Get("SELECT_todo2");

                    if (IsSimple.IsMatch(field.code))
                    {
                        foreach (var dbSource in dbSources)
                        {
                            if (dbSource == null)
                            {
                                continue;
                            }
                            var dbType = dbSource.ParseType(input.Scope.GetRegistry());
                            if (dbType is StructureType)
                            {
                                var found = ((StructureType)dbType).GetComponentByName(field.code);
                                if (found != null)
                                {
                                    type = found;
                                    break;
                                }
                            }
                        }
                    }

                    InlineData.RunSyntax(inline, input, type);
                }
            }
            return intoList;
        }

        return null;
    }

    private static AbstractType CountResultType(CurrentScope scope)
    {
        if (Versions.ReleaseAtLeast(scope.GetRelease(), Release.v750) || scope.GetOpenABAP() || scope.GetLanguageVersion() == LanguageVersion.Cloud)
        {
            return new Integer8Type();
        }
        else
        {
            return IntegerType.Get();
        }
    }

    private static AbstractType? BuildStructureType(List<(string code, string @as, ExpressionNode expression)> fields, List<IParseTypeObject?> dbSources, CurrentScope scope)
    {
        if (fields.Count == 1 && dbSources.Count == 1)
        {
            // COUNT( * ) yields an integer regardless of the (possibly dynamic) table
            if (Regex.IsMatch(fields.ElementAtOrDefault(0).code, @"^count\(\s*\*\s*\)$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase))
            {
                return Select.CountResultType(scope);
            }
            var dbType = dbSources.ElementAtOrDefault(0)?.ParseType(scope.GetRegistry());
            if (dbType == null)
            {
                var name = JsUtils.Or(dbSources.ElementAtOrDefault(0)?.GetName(), "buildStructureTypeError");
                if (scope.GetRegistry().InErrorNamespace(name) == true)
                {
                    return new UnknownType("Select, " + name + " not found");
                }
                else
                {
                    return VoidType.Get(dbSources.ElementAtOrDefault(0)?.GetName());
                }
            }
            if (fields.ElementAtOrDefault(0).code == "*")
            {
                return dbType;
            }
            else
            {
                if (dbType is StructureType)
                {
                    var field = ((StructureType)dbType).GetComponentByName(fields.ElementAtOrDefault(0).code);
                    if (field != null)
                    {
                        return field;
                    }
                    else if (fields.ElementAtOrDefault(0).code == "COUNT(*)")
                    {
                        if (Versions.ReleaseAtLeast(scope.GetRelease(), Release.v750) || scope.GetOpenABAP() || scope.GetLanguageVersion() == LanguageVersion.Cloud)
                        {
                            return new Integer8Type();
                        }
                        else
                        {
                            return IntegerType.Get();
                        }
                    }
                    else
                    {
                        // todo: aggregated/calculated values
                        return VoidType.Get("SELECT_todo11");
                    }
                }
                else
                {
                    return VoidType.Get("SELECT_todo10");
                }
            }
        }
        else if (dbSources.Count == 1)
        {
            var dbType = dbSources.ElementAtOrDefault(0)?.ParseType(scope.GetRegistry());
            if (dbType == null)
            {
                var name = JsUtils.Or(dbSources.ElementAtOrDefault(0)?.GetName(), "buildStructureTypeError");
                if (scope.GetRegistry().InErrorNamespace(name) == true)
                {
                    return new UnknownType("Select, " + name + " not found");
                }
                else
                {
                    return VoidType.Get(dbSources.ElementAtOrDefault(0)?.GetName());
                }
            }
            if (!((dbType is StructureType)))
            {
                return VoidType.Get("SELECT_todo14");
            }
            var allFieldsSimple = fields.All(f => IsSimple.IsMatch(f.code));
            if (allFieldsSimple == true)
            {
                List<IStructureComponent> components = new List<IStructureComponent>();
                foreach (var field in fields)
                {
                    var type = ((StructureType)dbType).GetComponentByName(field.code);
                    if (type == null)
                    {
                        return VoidType.Get("SELECT_todo9");
                    }
                    components.Add(new IStructureComponent { Name = JsUtils.Or(field.@as, field.code), Type = type });
                }
                return new StructureType(components);
            }
            return VoidType.Get("SELECT_todo12");
        }
        else
        {
            return VoidType.Get("SELECT_todo13");
        }
    }

    private static AbstractType BuildTableType(List<(string code, string @as, ExpressionNode expression)> fields, List<IParseTypeObject?> dbSources, CurrentScope scope, ExpressionNode from)
    {
        ITableOptions tableOptions = new ITableOptions { WithHeader = false, KeyType = TableKeyType.empty };

        if (dbSources.Count > 1)
        {
            var sourceInfos = Select.BuildDatabaseSourceInfos(from, dbSources);
            if (fields.Count == 1 && fields.ElementAtOrDefault(0).code == "*")
            {
                List<IStructureComponent> components2 = new List<IStructureComponent>();
                foreach (var info in sourceInfos)
                {
                    var type = info.Source?.ParseType(scope.GetRegistry());
                    if (!((type is StructureType)))
                    {
                        return VoidType.Get("SELECT_todo3");
                    }
                    components2.Add(new IStructureComponent { Name = JsUtils.Or(info.Alias, info.Name), Type = type });
                }
                return new global::ScipSharp.Indexer.Abap.Types.Basic.TableType(new StructureType(components2), tableOptions, null);
            }

            List<IStructureComponent> components = new List<IStructureComponent>();
            foreach (var field in fields)
            {
                var type = Select.FindFieldType(field.code, sourceInfos, scope);
                if (type == null)
                {
                    return VoidType.Get("SELECT_todo3");
                }
                var name = JsUtils.Or(field.@as, JsUtils.Pop(field.code.Split("~").ToList())!);
                components.Add(new IStructureComponent { Name = name, Type = type });
            }
            return new global::ScipSharp.Indexer.Abap.Types.Basic.TableType(new StructureType(components), tableOptions, null);
        }
        else if (dbSources.Count != 1)
        {
            return VoidType.Get("SELECT_todo3");
        }

        if (dbSources.ElementAtOrDefault(0) == null)
        {
            // then its a voided table
            return new global::ScipSharp.Indexer.Abap.Types.Basic.TableType(VoidType.Get("SELECT_todo4"), tableOptions, null);
        }
        var dbType = dbSources.ElementAtOrDefault(0).ParseType(scope.GetRegistry());
        if (!((dbType is StructureType)))
        {
            return VoidType.Get("SELECT_todo5");
        }

        if (fields.Count == 1 && fields.ElementAtOrDefault(0).code == "*")
        {
            return new global::ScipSharp.Indexer.Abap.Types.Basic.TableType(((StructureType)dbType), tableOptions, null);
        }

        var allFieldsSimple = fields.All(f => IsSimple.IsMatch(f.code));
        if (allFieldsSimple == true)
        {
            List<IStructureComponent> components = new List<IStructureComponent>();
            foreach (var field in fields)
            {
                var type = ((StructureType)dbType).GetComponentByName(field.code);
                if (type == null)
                {
                    return VoidType.Get("SELECT_todo6");
                }
                components.Add(new IStructureComponent { Name = JsUtils.Or(field.@as, field.code), Type = type });
            }
            return new global::ScipSharp.Indexer.Abap.Types.Basic.TableType(new StructureType(components), tableOptions, null);
        }

        return VoidType.Get("SELECT_todo7");
    }

    private static List<DatabaseSourceInfo> BuildDatabaseSourceInfos(ExpressionNode from, List<IParseTypeObject?> dbSources)
    {
        var fromSources = from.FindAllExpressions<SQLFromSource>();
        return (dbSources.Select((source, index) =>
        {
            var fromSource = fromSources.ElementAtOrDefault(index);
            var name = JsUtils.Or(JsUtils.ToUpper(JsUtils.RegexReplace(fromSource?.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.DatabaseTable>()?.GetFirstToken().GetStr(), @"^\*", "", RegexOptions.ECMAScript)), "");
            var alias = JsUtils.Or(JsUtils.ToUpper(fromSource?.FindDirectExpression<SQLAsName>()?.ConcatTokens()), "");
            return new DatabaseSourceInfo { Source = source, Name = name!, Alias = alias! };
        }).ToList());
    }

    private static AbstractType? FindFieldType(string code, List<DatabaseSourceInfo> sourceInfos, CurrentScope scope)
    {
        var separator = code.IndexOf("~", StringComparison.Ordinal);
        string? qualifier = separator >= 0 ? JsUtils.Substring(code, 0, separator) : (string?)null;
        string fieldName = separator >= 0 ? JsUtils.Substring(code, separator + 1) : code;
        List<DatabaseSourceInfo> candidates = qualifier == null ? sourceInfos : sourceInfos.Where(info => info.Alias == qualifier || info.Name == qualifier).ToList();

        foreach (var candidate in candidates)
        {
            AbstractType? type = candidate.Source?.ParseType(scope.GetRegistry());
            foreach (var component in fieldName.Split("-").ToList())
            {
                if (!((type is StructureType)))
                {
                    type = null;
                    break;
                }
                type = ((StructureType)type).GetComponentByName(component);
            }
            if (type != null)
            {
                return type;
            }
        }
        return null;
    }

    private static List<(string code, string @as, ExpressionNode expression)> FindFields(ExpressionNode node, SyntaxInput input)
    {
        var expr = node.FindFirstExpression<SQLFieldList>();
        List<(string code, string @as, ExpressionNode expression)> ret = new List<(string code, string @as, ExpressionNode expression)>();
        if ((expr?.GetFirstChild()?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic)
        {
            Dynamic.RunSyntax(((ExpressionNode)expr.GetFirstChild()), input);
        }

        // eslint-disable-next-line max-len
        foreach (var field in expr?.FindDirectExpressionsMulti(typeof(SQLField), typeof(SQLFieldName), typeof(SQLAggregation)) ?? new List<ExpressionNode>())
        {
            string code = JsUtils.ToUpper(field.ConcatTokens());
            var @as = JsUtils.Or(field.FindDirectExpression<SQLAsName>()?.ConcatTokens(), "");
            if (@as != "")
            {
                code = JsUtils.ReplaceFirst(code, " AS " + JsUtils.ToUpper(@as), "");
            }
            ret.Add((code, @as, field));
        }

        if (ret.Count == 0 && expr != null)
        {
            ret.Add((expr.ConcatTokens(), "", expr));
        }

        return ret;
    }
}
