// Ported from abaplint packages/core/src/abap/types/method_parameters.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Syntax;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Types;

// todo:
// this.exceptions = [];
// also consider RAISING vs EXCEPTIONS
public class MethodParameters : IMethodParameters
{

    // the keywords that can follow a parameter in each section, a plain parameter name is read as them
    internal static readonly Dictionary<string, List<string>> KEYWORDS_AFTER = new Dictionary<string, List<string>> { ["IMPORTING"] = new List<string> { "PREFERRED", "EXPORTING", "CHANGING", "RETURNING", "RAISING", "EXCEPTIONS" }, ["EXPORTING"] = new List<string> { "CHANGING", "RETURNING", "RAISING", "EXCEPTIONS" }, ["CHANGING"] = new List<string> { "RETURNING", "RAISING", "EXCEPTIONS" } };
    private string? preferred;
    private TypedIdentifier? returning;
    private readonly List<TypedIdentifier> importing;
    private readonly List<string> optional;
    private readonly List<TypedIdentifier> exporting;
    private readonly List<TypedIdentifier> changing;
    private readonly List<string> exceptions;
    private readonly Dictionary<string, ExpressionNode> defaults;
    private readonly string filename;

    public MethodParameters(StatementNode node, SyntaxInput input, bool abstractMethod)
    {
        if (!((node.Get() is MethodDef)))
        {
            throw new Exception("MethodDefinition, expected MethodDef as part of input node");
        }

        this.importing = new List<TypedIdentifier>();
        this.exporting = new List<TypedIdentifier>();
        this.changing = new List<TypedIdentifier>();
        this.optional = new List<string>();
        this.defaults = new Dictionary<string, ExpressionNode>();
        this.returning = null;
        this.preferred = null;
        this.exceptions = new List<string>();
        this.filename = input.Filename;

        // need the scope for LIKE typing inside method parameters
        var parentName = input.Scope.GetName();
        input.Scope.Push(ScopeType.MethodDefinition, "method definition", node.GetStart(), input.Filename);
        try
        {
            this.Parse(node, input, parentName, abstractMethod);
            this.CheckNameLengths(input);
            this.CheckDuplicateNames();
        }
        finally
        {
            input.Scope.Pop(node.GetEnd());
        }
    }

    public string GetFilename()
    {
        return this.filename;
    }

    public List<string> GetOptional()
    {
        return this.optional;
    }

    public List<TypedIdentifier> GetAll()
    {
        List<TypedIdentifier> ret = new List<TypedIdentifier>();
        var returning = this.GetReturning();
        if (returning != null)
        {
            ret.Add(returning);
        }
        ret.AddRange(this.GetImporting());
        ret.AddRange(this.GetExporting());
        ret.AddRange(this.GetChanging());
        return ret;
    }

    public string? GetDefaultImporting()
    {
        if (this.importing.Count == 0)
        {
            return null;
        }
        else if (this.importing.Count == 1)
        {
            return JsUtils.ToUpper(this.importing.ElementAtOrDefault(0).GetName());
        }
        else if (!string.IsNullOrEmpty(this.preferred))
        {
            return this.preferred;
        }

        List<string> candidates = this.importing.Select(i => JsUtils.ToUpper(i.GetName())).ToList();
        candidates = candidates.Where(c => this.optional.IndexOf(c) < 0).ToList();
        if (candidates.Count == 1)
        {
            return candidates.ElementAtOrDefault(0);
        }

        return null;
    }

    public List<TypedIdentifier> GetImporting()
    {
        return this.importing;
    }

    public List<TypedIdentifier> GetRequiredParameters()
    {
        List<TypedIdentifier> ret = new List<TypedIdentifier>();

        foreach (var i in this.GetImporting())
        {
            if (this.GetOptional().Any(o => JsUtils.ToUpper(o) == JsUtils.ToUpper(i.GetName())) == true)
            {
                continue;
            }
            else if (JsUtils.ToUpper(this.preferred) == JsUtils.ToUpper(i.GetName()))
            {
                continue;
            }
            ret.Add(i);
        }
        foreach (var i in this.GetChanging())
        {
            if (this.GetOptional().Any(o => JsUtils.ToUpper(o) == JsUtils.ToUpper(i.GetName())) == true)
            {
                continue;
            }
            ret.Add(i);
        }

        return ret;
    }

    public List<TypedIdentifier> GetExporting()
    {
        return this.exporting;
    }

    public List<TypedIdentifier> GetChanging()
    {
        return this.changing;
    }

    public TypedIdentifier? GetReturning()
    {
        return this.returning;
    }

    public List<string> GetExceptions()
    {
        return this.exceptions;
    }

    public ExpressionNode? GetParameterDefault(string parameter)
    {
        return this.defaults.GetValueOrDefault(JsUtils.ToUpper(parameter));
    }

    ///////////////////
    private void Parse(StatementNode node, SyntaxInput input, string parentName, bool abstractMethod)
    {

        var handler = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.EventHandler>();
        if (handler != null)
        {
            var nameToken = node.FindFirstExpression<ClassName>()?.GetFirstToken();
            var ooName = nameToken?.GetStr();
            var def = input.Scope.FindObjectDefinition(ooName);
            bool doVoid = def != null ? false : !input.Scope.GetDDIC().InErrorNamespace(ooName);
            if (def != null)
            {
                input.Scope.AddReference(nameToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)def), ReferenceType.ObjectOrientedReference, input.Filename);
            }
            else if (doVoid && !string.IsNullOrEmpty(ooName))
            {
                input.Scope.AddReference(nameToken, null, ReferenceType.ObjectOrientedVoidReference, this.filename, new IReferenceExtras { OoName = JsUtils.ToUpper(ooName) });
            }

            var eventName = node.FindFirstExpression<EventName>()?.GetFirstToken().GetStr();
            var @event = new ObjectOriented(input.Scope).SearchEvent(def, eventName);
            foreach (var p in handler.FindAllExpressions<MethodParamName>())
            {
                var token = p.GetFirstToken();
                var search = JsUtils.ReplaceFirst(JsUtils.ToUpper(token.GetStr()), "!", "");
                this.optional.Add(search);
                if (search == "SENDER" && def != null)
                {
                    this.importing.Add(new TypedIdentifier(token, this.filename, new ObjectReferenceType(((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)def)), new List<string> { IdentifierMeta.EventParameter }));
                    continue;
                }
                var found = @event?.GetParameters().FirstOrDefault(p2 => JsUtils.ToUpper(p2.GetName()) == search);
                if (found != null)
                {
                    this.importing.Add(new TypedIdentifier(token, this.filename, found.GetType(), new List<string> { IdentifierMeta.EventParameter }));
                }
                else if (doVoid)
                {
                    this.importing.Add(new TypedIdentifier(token, this.filename, VoidType.Get(ooName), new List<string> { IdentifierMeta.EventParameter }));
                }
                else
                {
                    var type = new UnknownType($"handler parameter not found \"{search}\"");
                    this.importing.Add(new TypedIdentifier(token, this.filename, type, new List<string> { IdentifierMeta.EventParameter }));
                }
            }
            return;
        }

        var importing = node.FindFirstExpression<MethodDefImporting>();
        if (importing != null)
        {
            this.Add(this.importing, importing, input, new List<string> { IdentifierMeta.MethodImporting }, abstractMethod);
            if (importing.FindDirectTokenByText("PREFERRED") != null)
            {
                this.preferred = JsUtils.ToUpper(importing.GetLastToken().GetStr());
                if (this.preferred.StartsWith("!", StringComparison.Ordinal))
                {
                    this.preferred = JsUtils.Substring(this.preferred, 1);
                }
            }
        }

        var exporting = node.FindFirstExpression<MethodDefExporting>();
        if (exporting != null)
        {
            this.Add(this.exporting, exporting, input, new List<string> { IdentifierMeta.MethodExporting }, abstractMethod);
        }

        var changing = node.FindFirstExpression<MethodDefChanging>();
        if (changing != null)
        {
            this.Add(this.changing, changing, input, new List<string> { IdentifierMeta.MethodChanging }, abstractMethod);
        }

        var returning = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodDefReturning>();
        if (returning != null)
        {
            this.returning = global::ScipSharp.Indexer.Abap.Syntax.Expressions.MethodDefReturning.RunSyntax(returning, input, new List<string> { IdentifierMeta.MethodReturning });
        }

        this.WorkaroundRAP(node, input, parentName);
    }

    private void CheckDuplicateNames()
    {
        var names = new HashSet<string>();
        foreach (var parameter in this.GetAll())
        {
            var name = JsUtils.ToUpper(parameter.GetName());
            if (names.Contains(name))
            {
                throw new Exception($"Method parameter \"{name}\" already defined");
            }
            names.Add(name);
        }
    }

    private void CheckNameLengths(SyntaxInput input)
    {
        foreach (var parameter in this.GetAll())
        {
            if (parameter.GetName().Length > 30)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, parameter.GetToken(), "Method parameter name longer than 30 characters"));
            }
        }
    }

    private void WorkaroundRAP(StatementNode node, SyntaxInput input, string parentName)
    {
        var resultName = node.FindExpressionAfterToken("RESULT");
        var concat = JsUtils.ToUpper(node.ConcatTokens());
        var isRap = node.FindExpressionAfterToken("IMPORTING") ?? (concat.Contains(" IMPORTING REQUEST ", StringComparison.Ordinal) ? node.FindExpressionAfterToken("REQUEST") : (ExpressionNode?)null);
        if (isRap != null)
        {
            // "FOR LOCK" handlers have a well defined signature, so the types can be derived
            string? lockEntity = concat.Contains(" FOR LOCK ", StringComparison.Ordinal) ? node.FindExpressionAfterToken("LOCK")?.ConcatTokens() : (string?)null;

            foreach (var foo in node.FindDirectExpressions<MethodParamName>())
            {
                if (foo == resultName)
                {
                    continue;
                }
                AbstractType type = lockEntity == null ? VoidType.Get("RapMethodParameter") : new BasicTypes(input).RapTableFor(lockEntity);
                this.importing.Add(new TypedIdentifier(foo.GetFirstToken(), input.Filename, type, new List<string> { IdentifierMeta.MethodImporting }));
            }

            if (lockEntity != null)
            {
                // note: "mapped" is not part of the signature for "FOR LOCK"
                var token = isRap.GetFirstToken();
                this.exporting.Add(new TypedIdentifier(new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(token.GetStart(), "failed"), input.Filename, VoidType.Get($"RESPONSE FOR FAILED EARLY {lockEntity}"), new List<string> { IdentifierMeta.MethodExporting }));
                this.exporting.Add(new TypedIdentifier(new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(token.GetStart(), "reported"), input.Filename, VoidType.Get($"RESPONSE FOR REPORTED EARLY {lockEntity}"), new List<string> { IdentifierMeta.MethodExporting }));
            }
            else if (concat.Contains(" FOR VALIDATE ", StringComparison.Ordinal) || concat.Contains(" FOR BEHAVIOR ", StringComparison.Ordinal) || concat.Contains(" FOR DETERMINE ", StringComparison.Ordinal) || concat.Contains(" FOR FEATURES ", StringComparison.Ordinal) || concat.Contains(" FOR INSTANCE FEATURES ", StringComparison.Ordinal) || concat.Contains(" FOR GLOBAL AUTHORIZATION ", StringComparison.Ordinal) || concat.Contains(" FOR PRECHECK ", StringComparison.Ordinal) || concat.Contains(" FOR NUMBERING ", StringComparison.Ordinal) || concat.Contains(" FOR READ ", StringComparison.Ordinal) || concat.Contains(" FOR VALIDATION ", StringComparison.Ordinal) || concat.Contains(" FOR MODIFY ", StringComparison.Ordinal))
            {
                var token = isRap.GetFirstToken();
                this.exporting.Add(new TypedIdentifier(new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(token.GetStart(), "failed"), input.Filename, VoidType.Get("RapMethodParameter"), new List<string> { IdentifierMeta.MethodExporting }));
                this.exporting.Add(new TypedIdentifier(new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(token.GetStart(), "mapped"), input.Filename, VoidType.Get("RapMethodParameter"), new List<string> { IdentifierMeta.MethodExporting }));
                this.exporting.Add(new TypedIdentifier(new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(token.GetStart(), "reported"), input.Filename, VoidType.Get("RapMethodParameter"), new List<string> { IdentifierMeta.MethodExporting }));
            }
        }

        if (resultName != null)
        {
            var token = resultName.GetFirstToken();
            this.importing.Add(new TypedIdentifier(token, input.Filename, VoidType.Get("RapMethodParameter"), new List<string> { IdentifierMeta.MethodExporting }));
        }

        // its some kind of magic
        if (JsUtils.ToUpper(parentName) == "CL_ABAP_BEHAVIOR_SAVER")
        {
            var tempChanging = this.changing.Select(c => new TypedIdentifier(c.GetToken(), input.Filename, VoidType.Get("RapMethodParameter"), c.GetMeta())).ToList();
            while (this.changing.Count > 0)
            {
                JsUtils.Shift(this.changing);
            }
            this.changing.AddRange(tempChanging);

            var tempImporting = this.importing.Select(c => new TypedIdentifier(c.GetToken(), input.Filename, VoidType.Get("RapMethodParameter"), c.GetMeta())).ToList();
            while (this.importing.Count > 0)
            {
                JsUtils.Shift(this.importing);
            }
            this.importing.AddRange(tempImporting);
        }
    }

    private void Add(List<TypedIdentifier> target, ExpressionNode source, SyntaxInput input, List<string> meta, bool abstractMethod)
    {
        this.CheckKeywordNames(source, input);
        foreach (var opt in source.FindAllExpressions<MethodParamOptional>())
        {
            var p = opt.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodParam>();
            if (p == null)
            {
                continue;
            }
            List<string> extraMeta = new List<string>();
            if (this.IsPassByValue(p))
            {
                extraMeta.Add(IdentifierMeta.PassByValue);
            }
            else if (meta.Contains(IdentifierMeta.MethodImporting))
            {
                extraMeta.Add(IdentifierMeta.ReadOnly);
            }
            if (abstractMethod == true)
            {
                extraMeta.Add(IdentifierMeta.Abstract);
            }
            var id = global::ScipSharp.Indexer.Abap.Syntax.Expressions.MethodParam.RunSyntax(p, input, new List<string>([.. meta, .. extraMeta]));
            input.Scope.AddIdentifier(id);
            target.Add(id);
            if (opt.FindDirectTokenByText("OPTIONAL") != null)
            {
                var name = JsUtils.ToUpper(target.ElementAtOrDefault(target.Count - 1).GetName());
                this.optional.Add(name);
            }
            else if ((opt.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Default>()) != null)
            {
                var name = JsUtils.ToUpper(target.ElementAtOrDefault(target.Count - 1).GetName());
                this.optional.Add(name);

                var val = opt.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Default>()?.GetLastChild();
                if (val != null && val is ExpressionNode)
                {
                    this.defaults[name] = ((ExpressionNode)val);
                }
            }
        }
        if (target.Count > 0)
        {
            return;
        }

        var @params = source.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodParam>();
        foreach (var param in @params)
        {
            List<string> extraMeta = this.IsPassByValue(param) ? new List<string> { IdentifierMeta.PassByValue } : new List<string>();
            target.Add(global::ScipSharp.Indexer.Abap.Syntax.Expressions.MethodParam.RunSyntax(param, input, new List<string>([.. meta, .. extraMeta])));
        }
    }

    // A system reads a plain parameter name as a keyword wherever that keyword can stand, measured on 758:
    // "val TYPE clike default TYPE i" makes default the DEFAULT addition of val, and then fails
    // with "Unable to interpret ..." on what follows. "!default" and "VALUE(default)" are names
    private void CheckKeywordNames(ExpressionNode section, SyntaxInput input)
    {
        var following = KEYWORDS_AFTER.GetValueOrDefault(JsUtils.ToUpper(section.GetFirstToken().GetStr()));
        if (following == null)
        {
            return;
        }
        // undefined: no parameter before in this section, true: the one before has OPTIONAL or DEFAULT
        bool? previousHasAddition = null;
        foreach (var child in section.GetChildren())
        {
            if (!((child is ExpressionNode)))
            {
                continue;
            }
            ExpressionNode? param = null;
            bool optional = false;
            if ((((ExpressionNode)child).Get()) is MethodParamOptional)
            {
                param = ((ExpressionNode)child).FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodParam>();
                optional = ((ExpressionNode)child).FindDirectTokenByText("OPTIONAL") != null;
            }
            else if ((((ExpressionNode)child).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodParam)
            {
                param = ((ExpressionNode)child);
            }
            else if ((((ExpressionNode)child).Get()) is MethodParamName)
            {
                // "TYPE ANY STRUCTURE", not measured, so never reported after it
                previousHasAddition = true;
                continue;
            }
            if (param == null)
            {
                continue;
            }

            var first = param.GetFirstToken();
            var name = JsUtils.ToUpper(first.GetStr());
            if ((param.GetChildren().ElementAtOrDefault(0)?.Get()) is MethodParamName)
            {
                string? readAs = null;
                if (following.Contains(name))
                {
                    readAs = "the " + (name == "PREFERRED" ? "PREFERRED PARAMETER" : name) + " addition";
                }
                else if ((name == "DEFAULT" || name == "OPTIONAL") && previousHasAddition == false)
                {
                    readAs = "the " + name + " addition of the parameter before it";
                }
                if (readAs != null)
                {
                    var message = "Parameter \"" + first.GetStr() + "\" is read as " + readAs + ", rename it or escape it as !" + first.GetStr();
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, first, message));
                }
            }

            previousHasAddition = optional || param.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Default>() != null;
        }
    }

    private bool IsPassByValue(ExpressionNode param)
    {
        // "!" is the identifier escape, generated code has eg. "IMPORTING !VALUE(iv_foo)"
        var first = JsUtils.ToUpper(param.GetFirstToken().GetStr());
        return (first == "VALUE" || first == "!VALUE") && param.GetChildren().ElementAtOrDefault(1)?.GetFirstToken().GetStr() == "(";
    }
}
