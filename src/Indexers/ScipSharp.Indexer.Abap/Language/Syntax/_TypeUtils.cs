// Ported from abaplint packages/core/src/abap/5_syntax/_type_utils.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax;

// todo: refactor to static? for performance
public class TypeUtils
{
    // scope is needed to determine class hieraracy for typing
    private readonly CurrentScope scope;

    public TypeUtils(CurrentScope scope)
    {
        this.scope = scope;
    }

    public bool IsCharLikeStrict(AbstractType? type)
    {
        if (type == null)
        {
            return false;
        }
        else if (type is StructureType)
        {
            foreach (var c in ((StructureType)type).GetComponents())
            {
                if (this.IsCharLikeStrict(c.Type) == false)
                {
                    return false;
                }
            }
            return true;
        }
        else if (type is TableType && ((TableType)type).IsWithHeader())
        {
            return this.IsCharLikeStrict(((TableType)type).GetRowType());
        }
        else if (type is StringType || type is AnyType || type is DataType || type is CharacterType || type is SimpleType || type is CGenericType || type is CLikeType || type is DateType || type is CSequenceType || type is NumericGenericType || type is NumericType || type is TimeType || type is UnknownType || type is VoidType)
        {
            return true;
        }
        return false;
    }

    public bool IsCharLikeField(AbstractType? type)
    {
        if (type is StructureType || (type is TableType && ((TableType)type).IsWithHeader()))
        {
            return this.IsCharLikeStrict(type);
        }
        return type is CharacterType || type is NumericType || type is DateType || type is TimeType || type is CGenericType || type is CLikeType || type is AnyType || type is UnknownType || type is VoidType;
    }

    public bool IsCharLikeForCompare(AbstractType? type)
    {
        return type == null || type is StringType || type is CLikeType || type is CSequenceType || type is DataType || this.IsCharLikeField(type);
    }

    public bool IsCharLike(AbstractType? type)
    {
        if (type == null)
        {
            return false;
        }
        else if (type is TableType && ((TableType)type).IsWithHeader())
        {
            return this.IsCharLike(((TableType)type).GetRowType());
        }
        else if (type is StructureType)
        {
            foreach (var c in ((StructureType)type).GetComponents())
            {
                if (this.IsCharLike(c.Type) == false)
                {
                    return false;
                }
            }
            return true;
        }
        else if (type is StringType || type is CharacterType || type is VoidType || type is AnyType || type is UnknownType || type is NumericType || type is IntegerType || type is Integer8Type || type is SimpleType || type is FloatType || type is FloatingPointType || type is DecFloatType || type is DecFloat16Type || type is DecFloat34Type || type is NumericGenericType || type is CSequenceType || type is CGenericType || type is DateType || type is DataType || type is CLikeType || type is PackedType || type is PGenericType || type is TimeType || type is EnumType)
        {
            return true;
        }
        return false;
    }

    public bool IsHexLike(AbstractType? type)
    {
        if (type == null)
        {
            return false;
        }
        else if (type is StructureType)
        {
            foreach (var c in ((StructureType)type).GetComponents())
            {
                if (this.IsHexLike(c.Type) == false)
                {
                    return false;
                }
            }
            return true;
        }
        else if (type is TableType && ((TableType)type).IsWithHeader())
        {
            return this.IsHexLike(((TableType)type).GetRowType());
        }
        else if (type is XStringType || type is HexType || type is VoidType || type is XGenericType || type is XSequenceType || type is AnyType || type is UnknownType)
        {
            return true;
        }
        return false;
    }

    public bool IsOOAssignable(ObjectReferenceType source, ObjectReferenceType target)
    {
        Identifier sid = source.GetIdentifier();
        Identifier tid = target.GetIdentifier();

        var tname = JsUtils.ToUpper(tid.GetName());
        var sname = JsUtils.ToUpper(sid.GetName());

        if (tname == sname)
        {
            return true;
        }
        /*
        console.dir(sid);
        console.dir(tid);
         */
        if (!((sid is ClassDefinition || sid is InterfaceDefinition)))
        {
            var found = this.scope.FindObjectDefinition(sid.GetName());
            if (found != null)
            {
                sid = ((Identifier?)found);
            }
            else
            {
                return false;
            }
        }

        if (!((tid is ClassDefinition || tid is InterfaceDefinition)))
        {
            var found = this.scope.FindObjectDefinition(tid.GetName());
            if (found != null)
            {
                tid = ((Identifier?)found);
            }
            else
            {
                return false;
            }
        }

        if (sid is ClassDefinition && tid is ClassDefinition)
        {
            if (sname == tname)
            {
                return true;
            }
            var slist = this.ListAllSupers(((ClassDefinition)sid));
            if (slist.IndexOf(tname) >= 0)
            {
                return true;
            }
        }
        else if (sid is ClassDefinition && tid is InterfaceDefinition)
        {
            if (((ClassDefinition)sid).GetImplementing().Any(i => i.Name == tname))
            {
                return true;
            }
            var slist = this.ListAllInterfaces(((ClassDefinition)sid));
            if (slist.IndexOf(tname) >= 0)
            {
                return true;
            }
        }
        else if (sid is InterfaceDefinition && tid is InterfaceDefinition)
        {
            if (sname == tname)
            {
                return true;
            }
            if (((InterfaceDefinition)sid).GetImplementing().Any(i => i.Name == tname))
            {
                return true;
            }
            var slist = this.ListAllInterfaces(((InterfaceDefinition)sid));
            if (slist.IndexOf(tname) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private List<string> ListAllInterfaces(IInterfaceDefinition cdef)
    {
        var ret = new HashSet<string>();
        List<string> stack = new List<string>();

        // initialize
        JsUtils.ForEach(cdef.GetImplementing(), i => stack.Add(i.Name));
        if (cdef is ClassDefinition)
        {
            var supers = this.ListAllSupers(((ClassDefinition)cdef));
            foreach (var s in supers)
            {
                JsUtils.ForEach(this.scope.FindClassDefinition(s)?.GetImplementing(), i => stack.Add(i.Name));
            }
        }

        // main loop
        while (stack.Count > 0)
        {
            var intf = JsUtils.ToUpper(JsUtils.Pop(stack)!);
            ret.Add(intf);

            var idef = this.scope.FindInterfaceDefinition(intf);
            JsUtils.ForEach(idef?.GetImplementing(), i => stack.Add(i.Name));
        }

        return ret.ToList();
    }

    private List<string> ListAllSupers(ClassDefinition cdef)
    {
        List<string> ret = new List<string>();
        string? sup = cdef.GetSuperClass();
        while (sup != null)
        {
            ret.Add(JsUtils.ToUpper(sup));
            sup = JsUtils.ToUpper(this.scope.FindClassDefinition(sup)?.GetSuperClass());
        }
        return ret;
    }

    public bool IsConvable(AbstractType? source, AbstractType? target)
    {

        if (source is TableType && target is TableType)
        {
            // table key conversion, eg DEFAULT to EMPTY
            return this.IsAssignable(((TableType)source).GetRowType(), ((TableType)target).GetRowType());
        }

        return this.IsAssignable(source, target);
    }

    public bool IsCastable(AbstractType? _source, AbstractType? _target)
    {
        // todo
        return true;
    }

    public bool IsCompareable(AbstractType? source1, AbstractType? source2, ExpressionNode node1, ExpressionNode node2)
    {
        /*
        console.dir(source1);
        console.dir(source2);
         */
        if (source1 == null || source2 == null)
        {
            return true;
        }

        if (source1 is HexType && this.IsCalculated(node1) && source2 is IntegerType)
        {
            return false;
        }
        if (source2 is HexType && this.IsCalculated(node2) && source1 is IntegerType)
        {
            return false;
        }

        if (source1 is DateType && this.IsCalculated(node2) && source2 is IntegerType)
        {
            return false;
        }
        if (source2 is DateType && this.IsCalculated(node1) && source1 is IntegerType)
        {
            return false;
        }

        return true;
    }

    private bool StructureContainsString(StructureType structure)
    {
        foreach (var c in structure.GetComponents())
        {
            if (c.Type is StringType)
            {
                return true;
            }
        }
        return false;
    }

    private bool StructureContainsVoid(StructureType structure)
    {
        foreach (var c in structure.GetComponents())
        {
            if (c.Type is VoidType)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsCalculated(ExpressionNode node)
    {
        /*
        if (node.getChildren().length === 1
        && node.get() instanceof Expressions.Source
        && node.getFirstChild()?.get() instanceof Expressions.MethodCallChain) {
        return false;
        }
         */
        if (node.GetChildren().Count == 4 && node.GetFirstChild() is TokenNodeRegex && JsUtils.ToUpper(node.GetFirstChild()?.ConcatTokens()) == "BOOLC")
        {
            return true;
        }

        if (node.GetChildren().Count >= 3)
        {
            var second = node.GetChildren().ElementAtOrDefault(1);
            if (second.GetFirstToken().GetStr() == "&&")
            {
                return true;
            }
        }

        var calculated = node.FindFirstExpression<MethodCallChain>() != null || node.FindFirstExpression<StringTemplate>() != null || node.FindFirstExpression<ArithOperator>() != null;
        return calculated;
    }

    // a character constant is only assignable to a hexadecimal target if it can be
    // interpreted as a hexadecimal value
    private bool IsHexConstant(ExpressionNode? node)
    {
        var constant = node?.ConcatTokens();
        if (constant == null)
        {
            return true;
        }
        if ((constant.StartsWith("'", StringComparison.Ordinal) && constant.EndsWith("'", StringComparison.Ordinal)) || (constant.StartsWith("`", StringComparison.Ordinal) && constant.EndsWith("`", StringComparison.Ordinal)))
        {
            var value = JsUtils.Substring(constant, 1, constant.Length - 1);
            return value.Length > 0 && Regex.IsMatch(value, @"^[0-9A-Fa-f]+$", RegexOptions.ECMAScript);
        }
        return true;
    }

    public bool IsAssignableNew(AbstractType? source, AbstractType? target, ExpressionNode? node)
    {
        var calculated = node?.FindFirstExpression<ArithOperator>() != null;

        if (calculated && source is HexType && target is IntegerType)
        {
            return false;
        }

        return this.IsAssignable(source, target);
    }

    public bool IsAssignableStrict(AbstractType? source, AbstractType? target, ExpressionNode? node = null)
    {
        bool calculated = node != null ? this.IsCalculated(node) : false;
        /*
        console.dir(source);
        console.dir(target);
        console.dir(calculated);
         */
        if (calculated && source is StringType && (target is XStringType || target is XSequenceType))
        {
            return false;
        }

        if (calculated && source is IntegerType && target is DateType)
        {
            return false;
        }

        if (target is NumericType && (source is CharacterType || source is StringType) && source.GetAbstractTypeData()?.DerivedFromConstant == true)
        {
            var constant = node?.ConcatTokens();
            if (constant != null && ((constant.StartsWith("'", StringComparison.Ordinal) && constant.EndsWith("'", StringComparison.Ordinal)) || (constant.StartsWith("`", StringComparison.Ordinal) && constant.EndsWith("`", StringComparison.Ordinal))) && Regex.IsMatch(JsUtils.Substring(constant, 1, constant.Length - 1), @"^\d*$", RegexOptions.ECMAScript) == false)
            {
                return false;
            }
        }

        if (calculated)
        {
            return this.IsAssignable(source, target);
        }

        if (target is CLikeType)
        {
            return this.IsCharLikeStrict(source);
        }

        if (target is PGenericType)
        {
            return source is PackedType || source is PGenericType || source is VoidType || source is AnyType || source is DataType || source is UnknownType;
        }

        if (source is CGenericType && target is StringType)
        {
            return false;
        }
        else if (source is CharacterType)
        {
            if (target is CharacterType)
            {
                if (((CharacterType)source).GetAbstractTypeData()?.DerivedFromConstant == true)
                {
                    return ((CharacterType)source).GetLength() <= ((CharacterType)target).GetLength();
                }
                return ((CharacterType)source).GetLength() == ((CharacterType)target).GetLength();
            }
            else if (target is IntegerType)
            {
                if (((CharacterType)source).GetAbstractTypeData()?.DerivedFromConstant == true)
                {
                    return true;
                }
                return false;
            }
            else if (target is XStringType)
            {
                if (((CharacterType)source).GetAbstractTypeData()?.DerivedFromConstant == true)
                {
                    return this.IsHexConstant(node);
                }
                return false;
            }
            else if (target is StringType)
            {
                if (((CharacterType)source).GetAbstractTypeData()?.DerivedFromConstant == true)
                {
                    return true;
                }
                return false;
            }
        }
        else if (source is HexType)
        {
            if (target is HexType)
            {
                if (((HexType)source).GetAbstractTypeData()?.DerivedFromConstant == true)
                {
                    return ((HexType)source).GetLength() <= ((HexType)target).GetLength();
                }
                return ((HexType)source).GetLength() == ((HexType)target).GetLength();
            }
            else if (target is XStringType)
            {
                return false;
            }
            else if (target is IntegerType || target is Integer8Type)
            {
                if (((HexType)source).GetAbstractTypeData()?.DerivedFromConstant == true)
                {
                    return true;
                }
                return false;
            }
        }
        else if (source is DateType || source is TimeType)
        {
            if (target is CharacterType || target is StringType)
            {
                return false;
            }
        }
        else if (source is StringType)
        {
            if (target is StructureType && this.StructureContainsString(((StructureType)target)))
            {
                return false;
            }
            else if (target is CharacterType)
            {
                if (((StringType)source).GetAbstractTypeData()?.DerivedFromConstant == true)
                {
                    return true;
                }
                return false;
            }
            else if (target is IntegerType)
            {
                if (((StringType)source).GetAbstractTypeData()?.DerivedFromConstant == true)
                {
                    return true;
                }
                return false;
            }
            else if (target is CGenericType || target is GenericObjectReferenceType)
            {
                return false;
            }
            else if (target is XSequenceType || target is XStringType)
            {
                if (((StringType)source).GetAbstractTypeData()?.DerivedFromConstant == true)
                {
                    return true;
                }
                return false;
            }
            else if (target is TableType || target is DataReference || target is ObjectReferenceType)
            {
                return false;
            }
            return true;
        }
        else if (source is StructureType)
        {
            if (target is StructureType)
            {
                var sourceComponents = ((StructureType)source).GetComponents();
                var targetComponents = ((StructureType)target).GetComponents();
                if (sourceComponents.Count != targetComponents.Count)
                {
                    return false;
                }
                for (var i = 0; i < sourceComponents.Count; i++)
                {
                    if (this.IsAssignableStrict(sourceComponents[i].Type, targetComponents.ElementAtOrDefault(i).Type) == false)
                    {
                        return false;
                    }
                }
                return true;
            }
            else if (target is SimpleType)
            {
                return this.IsCharLikeStrict(((StructureType)source));
            }
            else if (target is CLikeType)
            {
                return this.IsCharLikeStrict(((StructureType)source));
            }
            else if (target is VoidType || target is AnyType || target is DataType || target is UnknownType)
            {
                return true;
            }
            return false;
        }
        else if (source is Integer8Type)
        {
            if (target is IntegerType || target is StringType || target is FloatType)
            {
                return false;
            }
        }
        else if (source is IntegerType)
        {
            if (target is StringType || target is DateType || target is CharacterType)
            {
                return false;
            }
            else if (target is Integer8Type || target is PackedType)
            {
                if (((IntegerType)source).GetAbstractTypeData()?.DerivedFromConstant == true)
                {
                    return true;
                }
                return false;
            }
        }
        else if (source is PackedType && target is PackedType)
        {
            return ((PackedType)source).GetLength() == ((PackedType)target).GetLength() && ((PackedType)source).GetDecimals() == ((PackedType)target).GetDecimals();
        }
        else if (source is FloatType)
        {
            if (target is IntegerType)
            {
                return false;
            }
        }
        else if (source is GenericObjectReferenceType)
        {
            if (target is ObjectReferenceType || target is StringType || target is CharacterType || target is TableType || target is CLikeType)
            {
                return false;
            }
        }
        else if (source is ObjectReferenceType)
        {
            if (target is XSequenceType || target is IntegerType || target is StructureType || target is TableType || target is XStringType)
            {
                return false;
            }
        }
        else if (source is XStringType)
        {
            if (target is CharacterType || target is CLikeType || target is IntegerType || target is StringType || target is ObjectReferenceType || target is HexType)
            {
                return false;
            }
        }
        else if (source is TableType)
        {
            if ((((TableType)source).GetRowType()) is AnyType)
            {
                // then its a generic table, todo: add top level generic table type?
                return true;
            }
            else if (target is TableType && (((TableType)target).GetRowType()) is AnyType)
            {
                // then its a generic table, todo: add top level generic table type?
                return true;
            }
            else if (target is TableType)
            {
                var sourceKeyType = ((TableType)source).GetOptions().KeyType;
                var targetKeyType = ((TableType)target).GetOptions().KeyType;
                if (sourceKeyType != targetKeyType && sourceKeyType != TableKeyType.user && targetKeyType != TableKeyType.user)
                {
                    return false;
                }
                if (sourceKeyType == TableKeyType.empty && targetKeyType != TableKeyType.empty && !(((((TableType)target).GetRowType()) is AnyType)))
                {
                    return false;
                }
                return this.IsAssignableStrict(((TableType)source).GetRowType(), ((TableType)target).GetRowType());
            }
            else if (target is UnknownType || target is AnyType || target is VoidType)
            {
                return true;
            }
        }
        return this.IsAssignable(source, target);
    }

    // Typing with the classic STRUCTURE addition, ie. "FORM foo USING bar STRUCTURE zkey",
    // is not a compatibility check: ABAP only checks the fragment view up to the end of the
    // formal structure, so the actual parameter is allowed to be longer. For TABLES parameters
    // the table key is not part of the check at all.
    public bool IsAssignableStructureTyping(AbstractType? source, AbstractType? target, ExpressionNode? node = null)
    {
        if (source is StructureType && target is StructureType)
        {
            var sourceComponents = ((StructureType)source).GetComponents();
            var targetComponents = ((StructureType)target).GetComponents();
            if (sourceComponents.Count < targetComponents.Count)
            {
                return false;
            }
            for (var i = 0; i < targetComponents.Count; i++)
            {
                if (this.IsAssignableStrict(sourceComponents.ElementAtOrDefault(i).Type, targetComponents[i].Type) == false)
                {
                    return false;
                }
            }
            return true;
        }
        else if (source is TableType && target is TableType)
        {
            return this.IsAssignableStructureTyping(((TableType)source).GetRowType(), ((TableType)target).GetRowType());
        }

        return this.IsAssignableStrict(source, target, node);
    }

    public bool IsAssignable(AbstractType? source, AbstractType? target)
    {
        if (source == null || target == null)
        {
            return true;
        }

        /*
        console.dir(source);
        console.dir(target);
         */
        if (source is UTCLongType || target is UTCLongType)
        {
            return this.IsUTCLongAssignable(source, target);
        }

        if (target is TableType)
        {
            if (((TableType)target).IsWithHeader())
            {
                return this.IsAssignable(source, ((TableType)target).GetRowType());
            }
            if (source is VoidType || source is AnyType || source is DataType || source is UnknownType)
            {
                return true;
            }
            else if (source is TableType)
            {
                var targetRowType = ((TableType)target).GetRowType();
                var sourceRowType = ((TableType)source).GetRowType();
                if (targetRowType is VoidType || targetRowType is AnyType || targetRowType is UnknownType)
                {
                    return true;
                }
                else if (sourceRowType is VoidType || sourceRowType is AnyType || sourceRowType is UnknownType)
                {
                    return true;
                }
                if (targetRowType is StructureType && this.StructureContainsString(((StructureType)targetRowType)))
                {
                    if (!((sourceRowType is StructureType)))
                    {
                        return false;
                    }
                    else if (!this.StructureContainsString(((StructureType)sourceRowType)) && this.StructureContainsVoid(((StructureType)sourceRowType)) == false)
                    {
                        return false;
                    }
                }
                else if (sourceRowType is StructureType && this.StructureContainsString(((StructureType)sourceRowType)))
                {
                    if (!((targetRowType is StructureType)))
                    {
                        return false;
                    }
                    else if (!this.StructureContainsString(((StructureType)targetRowType)) && this.StructureContainsVoid(((StructureType)targetRowType)) == false)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
        else if (target is ObjectReferenceType && source is ObjectReferenceType)
        {
            return this.IsOOAssignable(((ObjectReferenceType)source), ((ObjectReferenceType)target));
        }
        else if (target is ObjectReferenceType || target is GenericObjectReferenceType)
        {
            if (source is ObjectReferenceType || source is GenericObjectReferenceType || source is VoidType || source is DataType || source is AnyType || source is UnknownType)
            {
                return true;
            }
            return false;
        }
        else if (target is DataReference)
        {
            if (source is DataReference || source is VoidType || source is DataType || source is AnyType || source is UnknownType)
            {
                return true;
            }
            return false;
        }
        else if (target is StructureType)
        {
            if (source is TableType && ((TableType)source).IsWithHeader())
            {
                return this.IsAssignable(((TableType)source).GetRowType(), ((StructureType)target));
            }
            else if (source is VoidType || source is AnyType || source is DataType || source is UnknownType)
            {
                return true;
            }
            else if (source is StructureType)
            {
                var targetDeep = this.StructureContainsString(((StructureType)target));
                var sourceDeep = this.StructureContainsString(((StructureType)source));
                if (targetDeep && !sourceDeep)
                {
                    return false;
                }

                var targetComponents = ((StructureType)target).GetComponents();
                var sourceComponents = ((StructureType)source).GetComponents();
                if (targetComponents.Count != sourceComponents.Count)
                {
                    if (targetDeep == true || sourceDeep == true)
                    {
                        return false;
                    }
                }

                for (var i = 0; i < targetComponents.Count; i++)
                {
                    if (sourceComponents.ElementAtOrDefault(i) == null)
                    {
                        continue;
                    }
                    // hmm
                    if ((sourceComponents.ElementAtOrDefault(i).Type) is StringType && !(((targetComponents[i].Type) is StringType)))
                    {
                        return false;
                    }
                    else if (!(((sourceComponents.ElementAtOrDefault(i).Type) is StringType)) && (targetComponents[i].Type) is StringType)
                    {
                        return false;
                    }
                }

                return true;
            }
            else if (((StructureType)target).ContainsVoid() == true)
            {
                return true;
            }
            else if (source is StringType && this.StructureContainsString(((StructureType)target)))
            {
                return false;
            }
            else if (source is IntegerType)
            {
                return false;
            }
            else if (this.IsCharLike(((StructureType)target)) && this.IsCharLike(source))
            {
                return true;
            }
            return false;
        }
        else if (target is IntegerType || target is CharacterType || target is StringType)
        {
            if (source is TableType && ((TableType)source).IsWithHeader() == false)
            {
                return false;
            }
            else if ((target is StringType || target is CharacterType) && source is StructureType && this.IsCharLike(((StructureType)source)))
            {
                if (this.StructureContainsString(((StructureType)source)) == true)
                {
                    return false;
                }
                return true;
            }
            else if (source is StructureType)
            {
                return this.IsCharLikeStrict(((StructureType)source));
            }
            else if (source is DataReference || source is ObjectReferenceType || source is GenericObjectReferenceType)
            {
                return false;
            }
            return true;
        }
        else if (target is SimpleType)
        {
            if (source is ObjectReferenceType || source is TableType)
            {
                return false;
            }
        }

        return true;
    }

    // utclong can only be converted to and from character like types
    private bool IsUTCLongAssignable(AbstractType source, AbstractType target)
    {
        if (this.IsGenericOrVoid(source) || this.IsGenericOrVoid(target))
        {
            return true;
        }
        else if (source is UTCLongType && target is UTCLongType)
        {
            return true;
        }
        else if (source is UTCLongType)
        {
            return this.IsTextLike(target);
        }
        return this.IsTextLike(source);
    }

    private bool IsGenericOrVoid(AbstractType type)
    {
        return type is VoidType || type is AnyType || type is DataType || type is UnknownType;
    }

    private bool IsTextLike(AbstractType type)
    {
        return type is CharacterType || type is StringType || type is CLikeType || type is CSequenceType || type is CGenericType || type is SimpleType;
    }
}
