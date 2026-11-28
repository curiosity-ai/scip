// Ported from abaplint packages/core/src/abap/5_syntax/expressions/source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

/*
 * Type interference, valid scenarios:
 * typed = VALUE #( ... ).         right hand side must follow left hand type
 * DATA(bar) = VALUE type( ... ).  left gets the type of rigthand
 * typed = VALUE type( ... ).      types must match and be compatible???
 ************* ERRORS *********
 * VALUE #( ... ).                 syntax error
 * DATA(bar) = VALUE #( ... ).     give error, no type can be derived
 */
// TODO: refactor all these method parameters to objects, this is getting messy
public class Source
{
    public static AbstractType? RunSyntax(ExpressionNode? node, SyntaxInput input, AbstractType? targetType = null, bool writeReference = false, bool allowGenericDeference = false, bool readReference = true)
    {
        // Not in abaplint: recursion over nested source, give up on the object rather than overflow the stack
        if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new InsufficientExecutionStackException();
        }

        if (node == null)
        {
            return null;
        }

        var children = node.GetChildren().ToList();
        IExpressionOrTokenNode? first = JsUtils.Shift(children);

        if (first is TokenNode)
        {
            var token = ((TokenNode)first).GetFirstToken();
            var tok = JsUtils.ToUpper(token.GetStr());
            switch (tok)
            {
                case "(":
                case "-":
                case "+":
                case "BIT":
                    break;
                case "BOOLC":
                {
                    var method = BuiltIn.SearchBuiltin(tok);
                    input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)method), ReferenceType.BuiltinMethodReference, input.Filename);
                    Cond.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Cond>()!, input);
                    return StringType.Get();
                }
                case "XSDBOOL":
                {
                    var method = BuiltIn.SearchBuiltin(tok);
                    input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)method), ReferenceType.BuiltinMethodReference, input.Filename);
                    Cond.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Cond>()!, input);
                    return new CharacterType(1, new AbstractTypeData { QualifiedName = "ABAP_BOOL", DdicName = "ABAP_BOOL" });
                }
                case "REDUCE":
                {
                    var foundType = Source.DetermineType(node, input, targetType);
                    var bodyType = ReduceBody.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ReduceBody>(), input, foundType);
                    if (foundType == null || foundType.IsGeneric())
                    {
                        Source.AddIfInferred(node, input, bodyType);
                    }
                    else
                    {
                        Source.AddIfInferred(node, input, foundType);
                    }
                    Source.TraverseRemainingChildren(children, input);
                    return foundType != null ? foundType : bodyType;
                }
                case "SWITCH":
                {
                    var foundType = Source.DetermineType(node, input, targetType);
                    var bodyType = SwitchBody.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SwitchBody>(), input, foundType);
                    if (foundType == null || foundType.IsGeneric())
                    {
                        Source.AddIfInferred(node, input, bodyType);
                    }
                    else
                    {
                        Source.AddIfInferred(node, input, foundType);
                    }
                    return foundType != null ? foundType : bodyType;
                }
                case "COND":
                {
                    var foundType = Source.DetermineType(node, input, targetType);
                    var bodyType = CondBody.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.CondBody>(), input, foundType);
                    /*
                    console.log("COND BODY type;:");
                    console.dir(bodyType);
                     */
                    if (foundType == null || foundType.IsGeneric())
                    {
                        Source.AddIfInferred(node, input, bodyType);
                    }
                    else
                    {
                        Source.AddIfInferred(node, input, foundType);
                    }
                    JsUtils.Shift(children);
                    JsUtils.Shift(children);
                    JsUtils.Shift(children);
                    JsUtils.Shift(children);
                    Source.TraverseRemainingChildren(children, input);
                    return foundType != null ? foundType : bodyType;
                }
                case "CONV":
                {
                    var foundType = Source.DetermineType(node, input, targetType);
                    var bodyType = ConvBody.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ConvBody>()!, input, foundType);
                    var inferred = node.FindDirectExpression<TypeNameOrInfer>()?.ConcatTokens();
                    if (new TypeUtils(input.Scope).IsConvable(foundType, bodyType) == false)
                    {
                        var message = $"CONV: Types not compatible, {(JsUtils.Str(foundType?.GetType().Name))}, {(JsUtils.Str(bodyType?.GetType().Name))}";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    else if ((foundType?.IsGeneric()) == true && inferred != "#")
                    {
                        var message = "Cannot CONV to generic type";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    if ((foundType?.IsGeneric()) == true && bodyType != null)
                    {
                        Source.AddIfInferred(node, input, bodyType);
                        Source.TraverseRemainingChildren(children, input);
                        return bodyType;
                    }
                    Source.AddIfInferred(node, input, foundType);
                    Source.TraverseRemainingChildren(children, input);
                    return foundType;
                }
                case "REF":
                {
                    AbstractType? foundType = Source.DetermineType(node, input, targetType);
                    var s = Source.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>(), input);
                    /*
                    console.dir(node.concatTokens());
                    console.dir(s);
                    console.dir(foundType);
                    console.dir(targetType);
                     */
                    if (foundType != null && foundType.IsGeneric() && s != null)
                    {
                        foundType = new DataReference(s);
                    }
                    else if (foundType == null && s != null)
                    {
                        if (s is AnyType)
                        {
                            foundType = new DataReference(VoidType.Get("REF-ANY"));
                        }
                        else
                        {
                            foundType = new DataReference(s);
                        }
                    }
                    else if (foundType != null && targetType == null)
                    {
                        foundType = new DataReference(foundType);
                    }

                    /*
                    if (targetType && !(targetType instanceof DataReference)) {
                    const message = `REF: Types not compatible, ` + targetType.constructor.name;
                    input.issues.push(syntaxIssue(input, node.getFirstToken(), message));
                    }
                     */
                    Source.AddIfInferred(node, input, foundType);
                    return foundType;
                }
                case "FILTER":
                {
                    var foundType = Source.DetermineType(node, input, targetType);
                    var bodyType = FilterBody.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FilterBody>(), input, foundType);
                    if (foundType == null || foundType.IsGeneric())
                    {
                        Source.AddIfInferred(node, input, bodyType);
                    }
                    else
                    {
                        Source.AddIfInferred(node, input, foundType);
                    }

                    if (foundType != null && !((foundType is UnknownType)))
                    {
                        return foundType;
                    }
                    else
                    {
                        return bodyType;
                    }
                }
                    goto case "CORRESPONDING";
                case "CORRESPONDING":
                {
                    var foundType = Source.DetermineType(node, input, targetType);
                    if (foundType?.IsGeneric() == true && node.FindDirectExpression<TypeNameOrInfer>()?.ConcatTokens() == "#" && !(Versions.ReleaseAtLeast(input.Scope.GetRelease(), Release.v756)) && input.Scope.GetLanguageVersion() != LanguageVersion.Cloud && !input.Scope.GetOpenABAP())
                    {
                        var message = "CORRESPONDING #, the type of the target is generic";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    CorrespondingBody.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.CorrespondingBody>(), input, foundType);
                    Source.AddIfInferred(node, input, foundType);
                    return foundType;
                }
                case "EXACT":
                {
                    var foundType = Source.DetermineType(node, input, targetType);
                    Source.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>(), input, foundType);
                    Source.AddIfInferred(node, input, foundType);
                    return foundType;
                }
                case "VALUE":
                {
                    var foundType = Source.DetermineType(node, input, targetType);
                    var bodyType = ValueBody.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ValueBody>(), input, foundType);
                    if (foundType == null || foundType.IsGeneric())
                    {
                        Source.AddIfInferred(node, input, bodyType);
                    }
                    else
                    {
                        Source.AddIfInferred(node, input, foundType);
                    }
                    return foundType != null ? foundType : bodyType;
                }
                default:
                    return new UnknownType("todo, Source type " + tok);
            }
        }
        else if (((ExpressionNode?)first) == null || !((first is ExpressionNode)))
        {
            return null;
        }

        AbstractType? context = new UnknownType("todo, Source type");

        List<string> type = new List<string>();
        if (readReference)
        {
            type.Add(ReferenceType.DataReadReference);
        }
        if (writeReference)
        {
            type.Add(ReferenceType.DataWriteReference);
        }

        bool hexExpected = false;
        bool hexNext = false;
        // an arithmetic operator has been seen, so the operands that follow
        // determine a calculation type rather than simply replacing the context
        bool arithmetic = false;
        while (children.Count >= 0)
        {
            if (first is ExpressionNode)
            {
                var get = ((ExpressionNode)first).Get();
                if (get is global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodCallChain)
                {
                    var found = MethodCallChain.RunSyntax(((ExpressionNode)first), input, targetType);
                    context = arithmetic == true ? Source.Infer(context, found, true) : found;
                    if (context == null)
                    {
                        var message = "Method has no RETURNING value";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                }
                else if (get is global::ScipSharp.Indexer.Abap.Statements.Expressions.Cast)
                {
                    var found = Cast.RunSyntax(((ExpressionNode)first), input, targetType);
                    context = arithmetic == true ? Source.Infer(context, found, true) : found;
                }
                else if (get is global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain)
                {
                    var found = FieldChain.RunSyntax(((ExpressionNode)first), input, type, allowGenericDeference);
                    context = arithmetic == true ? Source.Infer(context, found, true) : found;
                }
                else if (get is global::ScipSharp.Indexer.Abap.Statements.Expressions.StringTemplate)
                {
                    context = StringTemplate.RunSyntax(((ExpressionNode)first), input);
                }
                else if (get is global::ScipSharp.Indexer.Abap.Statements.Expressions.Source)
                {
                    var found = Source.RunSyntax(((ExpressionNode)first), input);
                    context = Source.Infer(context, found, arithmetic);
                }
                else if (get is global::ScipSharp.Indexer.Abap.Statements.Expressions.Constant)
                {
                    var found = Constant.RunSyntax(((ExpressionNode)first));
                    context = Source.Infer(context, found, arithmetic);
                }
                else if (get is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dereference)
                {
                    if (Source.IsNewObjectDereference(node, ((ExpressionNode)first)))
                    {
                        var message = "NEW cannot be dereferenced directly";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, ((ExpressionNode)first).GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    context = Dereference.RunSyntax(((ExpressionNode)first), context, input);
                }
                else if (get is global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentChain)
                {
                    context = ComponentChain.RunSyntax(context, ((ExpressionNode)first), input);
                }
                else if (get is ArithOperator)
                {
                    arithmetic = true;
                    if (((ExpressionNode)first).ConcatTokens() == "**")
                    {
                        context = new FloatType();
                    }
                    var @operator = JsUtils.ToUpper(((ExpressionNode)first).ConcatTokens());
                    if (@operator == "BIT-OR" || @operator == "BIT-AND" || @operator == "BIT-XOR")
                    {
                        hexExpected = true;
                        hexNext = true;
                    }
                }
                else if (get is global::ScipSharp.Indexer.Abap.Statements.Expressions.AttributeChain)
                {
                    context = AttributeChain.RunSyntax(context, ((ExpressionNode)first), input, type);
                }
            }

            if (hexExpected == true)
            {
                if (!((context is VoidType)) && !((context is XStringType)) && !((context is HexType)) && !((context is XGenericType)) && !((context is XSequenceType)) && !((context is UnknownType)))
                {
                    var message = "Operator only valid for XSTRING or HEX";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                if (hexNext == false)
                {
                    hexExpected = false;
                }
                hexNext = false;
            }

            first = JsUtils.Shift(children);
            if (first == null)
            {
                break;
            }
        }

        if (node.FindDirectTokenByText("&&") != null)
        {
            return StringType.Get();
        }

        return context;
    }

    ////////////////////////////////
    private static void TraverseRemainingChildren(List<IExpressionOrTokenNode> children, SyntaxInput input)
    {
        var last = children.ElementAtOrDefault(children.Count - 1);
        if (last != null && last.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Source)
        {
            Source.RunSyntax(((ExpressionNode)last), input);
        }
    }

    private static AbstractType? Infer(AbstractType? context, AbstractType? found, bool arithmetic = false)
    {
        if (arithmetic == true)
        {
            var calculation = Source.CalculationType(context, found);
            if (calculation != null)
            {
                return calculation;
            }
        }
        if (context is FloatType && found is IntegerType)
        {
            return ((FloatType)context);
        }
        else if (context is IntegerType && found is HexType)
        {
            return ((IntegerType)context);
        }
        else if ((context is IntegerType || context is FloatType) && (found?.IsGeneric()) == true)
        {
            return context;
        }
        else
        {
            return found;
        }
    }

    /** The calculation type of an arithmetic expression is the most general of the
     * operand types. An operand of type c, n, d, t or string is converted into it and
     * never becomes the result: "lv_f * '0.25'" is a float, not a character field of
     * length four. Returns undefined when neither operand is numeric, leaving the
     * decision to the caller. */
    private static AbstractType? CalculationType(AbstractType? left, AbstractType? right)
    {
        if (left == null || right == null)
        {
            return null;
        }
        else if (left is VoidType || left is UnknownType)
        {
            // not knowing an operand means not knowing the result
            return left;
        }
        else if (right is VoidType || right is UnknownType)
        {
            return right;
        }

        var leftRank = Source.NumericRank(left);
        var rightRank = Source.NumericRank(right);
        if (leftRank == null && rightRank == null)
        {
            return null;
        }
        else if (rightRank == null)
        {
            return left;
        }
        else if (leftRank == null)
        {
            return right;
        }
        return leftRank.Value >= rightRank.Value ? left : right;
    }

    private static int? NumericRank(AbstractType type)
    {
        if (type is DecFloat34Type)
        {
            return 6;
        }
        else if (type is DecFloat16Type)
        {
            return 5;
        }
        else if (type is FloatType)
        {
            return 4;
        }
        else if (type is PackedType)
        {
            return 3;
        }
        else if (type is Integer8Type)
        {
            return 2;
        }
        else if (type is IntegerType)
        {
            return 1;
        }
        return null;
    }

    public static void AddIfInferred(ExpressionNode node, SyntaxInput input, AbstractType? inferredType)
    {

        var basic = new BasicTypes(input);
        var typeExpression = node.FindDirectExpression<TypeNameOrInfer>();
        var typeToken = typeExpression?.GetFirstToken();
        //    const typeName = typeToken?.getStr();
        /*
        console.dir(inferredType);
        console.dir(typeToken);
         */
        if (inferredType != null && typeToken != null)
        {
            var found = basic.LookupQualifiedName(inferredType.GetQualifiedName());
            if (found != null)
            {
                var tid = new TypedIdentifier(typeToken, input.Filename, inferredType);
                input.Scope.AddReference(typeToken, tid, ReferenceType.InferredType, input.Filename, new IReferenceExtras { FoundQualified = true });
            }
            else if (inferredType is ObjectReferenceType)
            {
                var def = input.Scope.FindObjectDefinition(((ObjectReferenceType)inferredType).GetQualifiedName());
                var tid = new TypedIdentifier(typeToken, input.Filename, ((ObjectReferenceType)inferredType));
                if (def != null)
                {
                    input.Scope.AddReference(typeToken, tid, ReferenceType.InferredType, input.Filename, new IReferenceExtras { FoundQualified = true });
                }
                else
                {
                    input.Scope.AddReference(typeToken, tid, ReferenceType.InferredType, input.Filename, new IReferenceExtras { FoundQualified = false });
                }
            }
            else
            {
                var tid = new TypedIdentifier(typeToken, input.Filename, inferredType);
                input.Scope.AddReference(typeToken, tid, ReferenceType.InferredType, input.Filename, new IReferenceExtras { FoundQualified = false });
            }
        }
    }

    private static AbstractType? DetermineType(ExpressionNode node, SyntaxInput input, AbstractType? targetType)
    {

        var basic = new BasicTypes(input);
        var typeExpression = node.FindDirectExpression<TypeNameOrInfer>();
        var typeToken = typeExpression?.GetFirstToken();
        var typeName = typeToken?.GetStr();

        if (typeExpression == null)
        {
            throw new AssertError("determineType, child TypeNameOrInfer not found");
        }
        else if (typeName == "#" && targetType != null)
        {
            return targetType;
        }

        if (typeName != "#" && typeToken != null)
        {
            var found = basic.ParseType(typeExpression);
            if (found != null && found is UnknownType)
            {
                if (input.Scope.GetDDIC().InErrorNamespace(typeName) == false)
                {
                    input.Scope.AddReference(typeToken, null, ReferenceType.VoidType, input.Filename);
                    return VoidType.Get(typeName);
                }
                else
                {
                    var tid = new TypedIdentifier(typeToken, input.Filename, ((UnknownType)found));
                    input.Scope.AddReference(typeToken, tid, ReferenceType.TypeReference, input.Filename);
                    return ((UnknownType)found);
                }
            }
            else if (found == null)
            {
                var message = "Type \"" + JsUtils.Str(typeName) + "\" not found in scope, VALUE";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }

            return found;
        }

        return targetType;
    }

    // "NEW ty( )->*" is a syntax error, also in releases where a method call can be dereferenced
    private static bool IsNewObjectDereference(ExpressionNode node, ExpressionNode deref)
    {
        var children = node.GetChildren();
        var before = children.ElementAtOrDefault(JsUtils.IndexOf(children, deref) - 1);
        return before is ExpressionNode && (((ExpressionNode)before).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodCallChain && ((ExpressionNode)before).GetChildren().Count == 1 && (((ExpressionNode)before).GetFirstChild()?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.NewObject;
    }
}
