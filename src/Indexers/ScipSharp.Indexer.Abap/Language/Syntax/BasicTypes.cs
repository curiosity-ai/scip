// Ported from abaplint packages/core/src/abap/5_syntax/basic_types.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax;

public class BasicTypes
{
    private readonly SyntaxInput input;

    public BasicTypes(SyntaxInput input)
    {
        this.input = input;
    }

    public TypedIdentifier? LookupQualifiedName(string? name)
    {
        // argh, todo, rewrite this entire method, more argh, again argh
        if (name == null)
        {
            return null;
        }

        var found = this.input.Scope.FindType(name);
        if (found != null)
        {
            return found;
        }

        if (name.Contains("=>", StringComparison.Ordinal))
        {
            var split = name.Split("=>").ToList();
            var ooName = split.ElementAtOrDefault(0);
            var typeName = split.ElementAtOrDefault(1);
            var oo = this.input.Scope.FindObjectDefinition(ooName);
            if (oo != null)
            {
                if (typeName.Contains("-", StringComparison.Ordinal))
                {
                    var split2 = typeName.Split("-").ToList();
                    var subTypeName = split2.ElementAtOrDefault(0);
                    var fieldName = split2.ElementAtOrDefault(1);
                    var stru = oo.GetTypeDefinitions().GetByName(subTypeName);
                    var struType = stru?.GetType();
                    if (stru != null && struType is StructureType)
                    {
                        AbstractType? f = ((StructureType)struType).GetComponentByName(fieldName);
                        if (!string.IsNullOrEmpty(split2.ElementAtOrDefault(2)) && f is StructureType)
                        {
                            f = ((StructureType)f).GetComponentByName(split2.ElementAtOrDefault(2));
                        }
                        if (f != null)
                        {
                            return new TypedIdentifier(stru.GetToken(), stru.GetFilename(), f);
                        }
                    }
                }
                else
                {
                    var f = oo.GetTypeDefinitions().GetByName(typeName);
                    if (f != null)
                    {
                        return f;
                    }
                }
            }
        }
        else if (name.Contains("-", StringComparison.Ordinal))
        {
            var split = name.Split("-").ToList();
            var typeName = split.ElementAtOrDefault(0);
            var fieldName = split.ElementAtOrDefault(1);
            var type2 = this.input.Scope.FindType(typeName);
            if (type2 != null)
            {
                var stru = type2.GetType();
                if (stru is StructureType)
                {
                    AbstractType? f = ((StructureType)stru).GetComponentByName(fieldName);
                    if (!string.IsNullOrEmpty(split.ElementAtOrDefault(2)) && f is StructureType)
                    {
                        f = ((StructureType)f).GetComponentByName(split.ElementAtOrDefault(2));
                    }
                    if (f != null)
                    {
                        return new TypedIdentifier(type2.GetToken(), type2.GetFilename(), f);
                    }
                }
            }
        }

        var lookup = this.input.Scope.GetDDIC().LookupNoVoid(name);
        var id = lookup?.Object?.GetIdentifier();
        if (id != null && (lookup?.Type) != null)
        {
            return new TypedIdentifier(id.GetToken(), id.GetFilename(), lookup.Type);
        }

        var builtin = this.input.Scope.GetDDIC().LookupBuiltinType(name);
        if (builtin != null)
        {
            return new TypedIdentifier(new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(1, 1), name), BuiltIn.filename, builtin);
        }

        var type = this.input.Scope.FindTypePoolType(name);
        if (type != null)
        {
            return type;
        }

        return null;
    }

    public AbstractType? ResolveLikeName(INode? node, bool headerLogic = true)
    {
        if (node == null)
        {
            return null;
        }

        ExpressionNode? chain = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>();
        if (chain == null)
        {
            chain = node.FindFirstExpression<TypeName>();
        }
        if (chain == null)
        {
            chain = node.FindFirstExpression<FieldSub>();
        }
        if (chain == null)
        {
            chain = node.FindFirstExpression<SimpleFieldChain>();
        }
        if (chain == null)
        {
            chain = node.FindFirstExpression<SimpleFieldChain2>();
        }
        if (chain == null)
        {
            throw new Exception("resolveLikeName, chain undefined");
        }

        var fullName = chain.ConcatTokens();
        List<IExpressionOrTokenNode> children = new List<IExpressionOrTokenNode>([.. chain.GetChildren()]);

        if (children.Count == 0)
        {
            return new UnknownType("Type error, could not resolve \"" + fullName + "\", resolveLikeName1");
        }

        AbstractType? type = null;
        if (children.ElementAtOrDefault(1) != null && (children.ElementAtOrDefault(1).GetFirstToken().GetStr() == "=>" || children.ElementAtOrDefault(1).GetFirstToken().GetStr() == "->"))
        {
            type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.FieldChain.RunSyntax(chain, this.input, ReferenceType.TypeReference);
        }
        else
        {
            var name = JsUtils.Shift(children)!.GetFirstToken().GetStr();
            TypedIdentifier? found = this.input.Scope.FindVariable(name);

            var full = this.input.Scope.FindVariable(fullName);
            if (full != null)
            {
                children = new List<IExpressionOrTokenNode>();
                found = full;
            }

            type = found?.GetType();

            if (found == null)
            {
                found = this.input.Scope.FindExtraLikeType(name);
                type = found?.GetType();
            }

            if (found != null)
            {
                this.input.Scope.AddReference(chain?.GetFirstToken(), found, ReferenceType.TypeReference, this.input.Filename);
            }

            if (type == null)
            {
                var found2 = this.input.Scope.GetDDIC().LookupNoVoid(name);
                if (found2 != null)
                {
                    IObjectAndToken @using = new IObjectAndToken { Filename = this.input.Filename, Token = chain.GetFirstToken(), Object = found2.Object };
                    this.input.Scope.GetDDICReferences().AddUsing(this.input.Scope.GetParentObj(), @using);
                }
                type = found2?.Type;
            }

            if (type == null && this.input.Scope.IsAnyOO() == false && this.input.Scope.GetDDIC().InErrorNamespace(name) == false)
            {
                this.input.Scope.AddReference(chain.GetChildren().ElementAtOrDefault(0).GetFirstToken(), null, ReferenceType.VoidType, this.input.Filename);
                return VoidType.Get(name);
            }

            while (children.Count > 0)
            {
                var child = JsUtils.Shift(children)!;

                if (child.GetFirstToken().GetStr() == "-")
                {
                    if (type is VoidType)
                    {
                        return ((VoidType)type);
                    }
                }
                else if (child.ConcatTokens() == "[]")
                {
                    if (type is TableType)
                    {
                        type = new TableType(((TableType)type).GetRowType(), new ITableOptions { WithHeader = false, KeyType = TableKeyType.@default });
                    }
                }
                else if (child is ExpressionNode && (((ExpressionNode)child).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldLength)
                {
                    var len = JsUtils.ParseInt(JsUtils.ReplaceFirst(JsUtils.ReplaceFirst(((ExpressionNode)child).ConcatTokens(), "(", ""), ")", ""), 10);
                    if (JsUtils.IsNaN(len))
                    {
                        type = new UnknownType("Type error, invalid field length");
                    }
                    else
                    {
                        if (type is DateType)
                        {
                            if (len > 8)
                            {
                                type = new UnknownType("Type error, offset too long");
                            }
                            else
                            {
                                type = new CharacterType(len);
                            }
                        }
                        else if (type is CharacterType)
                        {
                            if (len <= ((CharacterType)type).GetLength())
                            {
                                type = new CharacterType(len);
                            }
                            else
                            {
                                type = new UnknownType("Type error, offset too long");
                            }
                        }
                    }
                }
                else
                {
                    if (type is TableType)
                    {
                        type = ((TableType)type).GetRowType();
                    }
                    if (type is StructureType)
                    {
                        type = ((StructureType)type).GetComponentByName(child.GetFirstToken().GetStr());
                        if (type == null)
                        {
                            return new UnknownType("Type error, field not part of structure " + fullName);
                        }
                    }
                    else if (!((type is VoidType)))
                    {
                        return new UnknownType("Type error, field not part of structure " + fullName);
                    }
                }
            }

            if (type is VoidType)
            {
                return ((VoidType)type);
            }
            else if (type is TableType && ((TableType)type).IsWithHeader() && headerLogic == true)
            {
                type = ((TableType)type).GetRowType();
            }
            else if (type is TableType && ((TableType)type).IsWithHeader() == true && (((TableType)type).GetRowType()) is VoidType)
            {
                return ((TableType)type).GetRowType();
            }
        }

        if (!(type != null))
        {
            return new UnknownType("Type error, could not resolve \"" + fullName + "\", resolveLikeName2");
        }

        return type;
    }

    private AbstractType CloneType(AbstractType type, string? qualifiedName = null)
    {
        // nested types(containing "-") will inherit the qualified names if possible
        // todo, this needs to be extended to all AbstractTypes instead of just CharacterType
        if (type is CharacterType && !string.IsNullOrEmpty(qualifiedName) && qualifiedName.Contains("-", StringComparison.Ordinal) == false)
        {
            type = ((CharacterType)type).CloneType((qualifiedName, default, default));
        }
        return type;
    }

    public AbstractType? ResolveTypeName(ExpressionNode? typeName, int? length = null, int? decimals = null, string? qualifiedName = null)
    {

        if (typeName == null)
        {
            return null;
        }

        var chain = this.ResolveTypeChain(typeName);
        if (chain != null)
        {
            return this.CloneType(chain, qualifiedName);
        }

        var chainText = JsUtils.ToUpper(typeName.ConcatTokens());
        var f = this.input.Scope.GetDDIC().LookupBuiltinType(chainText, length, decimals, qualifiedName);
        if (f != null)
        {
            return f;
        }

        var typ = this.input.Scope.FindType(chainText);
        if (typ != null)
        {
            var token = typeName.GetFirstToken();

            if (chainText.Contains("~", StringComparison.Ordinal))
            {
                var name = chainText.Split("~").ToList().ElementAtOrDefault(0);
                var idef = this.input.Scope.FindInterfaceDefinition(name);
                if (idef != null)
                {
                    this.input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)idef), ReferenceType.ObjectOrientedReference, this.input.Filename, new IReferenceExtras { OoType = "INTF", OoName = name });
                }
            }

            this.input.Scope.AddReference(token, typ, ReferenceType.TypeReference, this.input.Filename);
            return this.CloneType(typ.GetType(), qualifiedName);
        }

        var type = this.input.Scope.FindTypePoolType(chainText)?.GetType();
        if (type != null)
        {
            //      this.scope.addReference(typeName.getFirstToken(), type, ReferenceType.TypeReference, this.filename);
            return type;
        }

        var ddic = this.input.Scope.GetDDIC().Lookup(chainText);
        if (ddic != null)
        {
            this.input.Scope.GetDDICReferences().AddUsing(this.input.Scope.GetParentObj(), new IObjectAndToken { Object = ddic.Object, Token = typeName.GetFirstToken(), Filename = this.input.Filename });
            if (((object)ddic.Type) is TypedIdentifier)
            {
                this.input.Scope.AddReference(typeName.GetFirstToken(), (TypedIdentifier)(object)ddic.Type, ReferenceType.TypeReference, this.input.Filename);
            }
            else if (ddic.Type is VoidType)
            {
                this.input.Scope.AddReference(typeName.GetFirstToken(), null, ReferenceType.VoidType, this.input.Filename);
            }

            return this.CloneType(ddic.Type, qualifiedName);
        }

        return null;
    }

    public TypedIdentifier? SimpleType(INode node, string? qualifiedNamePrefix = null)
    {
        ExpressionNode? nameExpr = node.FindFirstExpression<NamespaceSimpleName>();
        if (nameExpr == null)
        {
            nameExpr = node.FindFirstExpression<DefinitionName>();
        }
        if (nameExpr == null)
        {
            return null;
        }
        AbstractToken name = nameExpr.GetFirstToken();
        if (nameExpr.CountTokens() > 1)
        {
            name = new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(name.GetStart(), nameExpr.ConcatTokens());
        }

        string? qualifiedName = null;
        if (node.Get() is global::ScipSharp.Indexer.Abap.Statements.Statements.Type)
        {
            if (this.input.Scope.IsTypePool() == true)
            {
                qualifiedName = name.GetStr();
            }
            else
            {
                qualifiedName = (JsUtils.Or(qualifiedNamePrefix, "")) + name.GetStr();
                if (this.input.Scope.GetType() == ScopeType.ClassDefinition || this.input.Scope.GetType() == ScopeType.Interface)
                {
                    qualifiedName = this.input.Scope.GetName() + "=>" + qualifiedName;
                }
            }
        }
        else if (!string.IsNullOrEmpty(qualifiedNamePrefix))
        {
            qualifiedName = qualifiedNamePrefix + JsUtils.Str(qualifiedName);
        }

        var found = this.ParseType(node, qualifiedName);
        if (found != null)
        {
            return new TypedIdentifier(name, this.input.Filename, found);
        }

        return null;
    }

    public AbstractType? ParseTable(INode node, string? name = null)
    {
        var typename = node.FindFirstExpression<TypeName>() ?? node.FindFirstExpression<EntityAssociation>();

        var text = JsUtils.ToUpper(node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.TypeTable>()?.ConcatTokens());
        if (text == null)
        {
            return null;
        }

        string? type = null;
        if (text.StartsWith("TYPE STANDARD TABLE ", StringComparison.Ordinal) || text.StartsWith("TYPE TABLE ", StringComparison.Ordinal) || text.StartsWith("LIKE TABLE ", StringComparison.Ordinal) || text.StartsWith("LIKE STANDARD TABLE ", StringComparison.Ordinal))
        {
            type = TableAccessType.standard;
        }
        else if (text.StartsWith("TYPE SORTED TABLE ", StringComparison.Ordinal) || text.StartsWith("LIKE SORTED TABLE ", StringComparison.Ordinal))
        {
            type = TableAccessType.sorted;
        }
        else if (text.StartsWith("TYPE HASHED TABLE ", StringComparison.Ordinal) || text.StartsWith("LIKE HASHED TABLE ", StringComparison.Ordinal))
        {
            type = TableAccessType.hashed;
        }

        var typeTableKeys = node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.TypeTableKey>();

        var firstKey = typeTableKeys.ElementAtOrDefault(0);
        var isNamed = firstKey?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Field>() != null && JsUtils.ToUpper(firstKey?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Field>()?.ConcatTokens()) != "PRIMARY_KEY";
        ITableKey primaryKey = new ITableKey { Name = "primary_key", Type = JsUtils.Or(type, TableAccessType.standard), IsUnique = isNamed ? false : JsUtils.ToUpper(firstKey?.ConcatTokens())?.Contains("WITH UNIQUE ", StringComparison.Ordinal) == true, KeyFields = new List<string>() };
        int start = 1;
        if (isNamed == false)
        {
            foreach (var k in firstKey?.FindDirectExpressions<FieldSub>() ?? new List<ExpressionNode>())
            {
                primaryKey.KeyFields.Add(JsUtils.ToUpper(k.ConcatTokens()));
            }
        }
        else
        {
            start = 0;
        }

        List<ITableKey> secondaryKeys = new List<ITableKey>();
        for (var i = start; i < typeTableKeys.Count; i++)
        {
            var row = typeTableKeys[i];
            var name2 = row.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Field>()?.ConcatTokens();
            if (name2 == null)
            {
                continue;
            }

            ITableKey secondary = new ITableKey { Name = name2, Type = row.FindDirectTokenByText("SORTED") != null ? TableAccessType.sorted : TableAccessType.hashed, IsUnique = JsUtils.ToUpper(row?.ConcatTokens())?.Contains("WITH UNIQUE ", StringComparison.Ordinal) == true, KeyFields = new List<string>() };

            foreach (var k in row?.FindDirectExpressions<FieldSub>() ?? new List<ExpressionNode>())
            {
                secondary.KeyFields.Add(JsUtils.ToUpper(k.ConcatTokens()));
            }

            secondaryKeys.Add(secondary);
        }

        string keyType = TableKeyType.user;
        if (text.Contains(" EMPTY KEY", StringComparison.Ordinal))
        {
            keyType = TableKeyType.empty;
        }
        else if (text.Contains(" DEFAULT KEY", StringComparison.Ordinal))
        {
            keyType = TableKeyType.@default;
        }

        ITableOptions options = new ITableOptions { WithHeader = text.Contains(" WITH HEADER LINE", StringComparison.Ordinal), KeyType = keyType, PrimaryKey = primaryKey, Secondary = secondaryKeys };

        AbstractType? found = null;
        if (text.StartsWith("TYPE TABLE OF REF TO ", StringComparison.Ordinal) || text.StartsWith("TYPE STANDARD TABLE OF REF TO ", StringComparison.Ordinal) || text.StartsWith("TYPE SORTED TABLE OF REF TO ", StringComparison.Ordinal) || text.StartsWith("TYPE HASHED TABLE OF REF TO ", StringComparison.Ordinal))
        {
            found = this.ResolveTypeRef(typename);
            if (found != null)
            {
                return new TableType(found, options, name);
            }
        }
        else if (text.StartsWith("TYPE TABLE OF ", StringComparison.Ordinal) || text.StartsWith("TYPE STANDARD TABLE OF ", StringComparison.Ordinal) || text.StartsWith("TYPE SORTED TABLE OF ", StringComparison.Ordinal) || text.StartsWith("TYPE HASHED TABLE OF ", StringComparison.Ordinal))
        {
            found = this.ResolveTypeName(typename);
            if (found != null)
            {
                return new TableType(found, options, name);
            }
        }
        else if (text.StartsWith("LIKE TABLE OF ", StringComparison.Ordinal) || text.StartsWith("LIKE STANDARD TABLE OF ", StringComparison.Ordinal) || text.StartsWith("LIKE SORTED TABLE OF ", StringComparison.Ordinal) || text.StartsWith("LIKE HASHED TABLE OF ", StringComparison.Ordinal))
        {
            found = this.ResolveLikeName(node);
            if (found != null)
            {
                return new TableType(found, options, name);
            }
        }
        else if (text == "TYPE STANDARD TABLE" || text == "TYPE SORTED TABLE" || text == "TYPE HASHED TABLE" || text == "TYPE INDEX TABLE" || text == "TYPE ANY TABLE")
        {
            return new TableType(AnyType.Get(), options);
        }
        else if (text.StartsWith("TYPE RANGE OF ", StringComparison.Ordinal))
        {
            var sub = node.FindFirstExpression<TypeName>();
            found = this.ResolveTypeName(sub);
            if (found == null)
            {
                return new UnknownType("TYPE RANGE OF, could not resolve type");
            }
            var structure = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "sign", Type = new CharacterType(1) }, new IStructureComponent { Name = "option", Type = new CharacterType(2) }, new IStructureComponent { Name = "low", Type = found }, new IStructureComponent { Name = "high", Type = found } });
            options.PrimaryKey!.Type = TableAccessType.standard;
            return new TableType(structure, options, name);
        }
        else if (text.StartsWith("LIKE RANGE OF ", StringComparison.Ordinal))
        {
            var sub = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>();
            found = this.ResolveLikeName(sub);
            if (found == null)
            {
                return new UnknownType("LIKE RANGE OF, could not resolve type");
            }
            var structure = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "sign", Type = new CharacterType(1) }, new IStructureComponent { Name = "option", Type = new CharacterType(2) }, new IStructureComponent { Name = "low", Type = found }, new IStructureComponent { Name = "high", Type = found } }, name);
            options.PrimaryKey!.Type = TableAccessType.standard;
            return new TableType(structure, options);
        }
        else if (typename != null && this.IsRAPTableFor(text))
        {
            return this.RapTableFor(typename.ConcatTokens(), options);
        }

        // fallback to old style syntax, OCCURS etc
        return this.ParseType(node, name);
    }

    /** type of a RAP derived table type, ie the "TABLE FOR LOCK zentity" part of a type */
    public AbstractType RapTableFor(string entityName, ITableOptions? options = null)
    {
        var ddlsName = this.ResolveRAPAlias(this.GetRAPBaseEntityName(entityName));
        if ((this.input.Scope.GetDDIC().LookupDDLS(ddlsName)?.type) != null)
        {
            return new TableType(VoidType.Get("RAP-TODO"), options ?? new ITableOptions { WithHeader = false, KeyType = TableKeyType.user, PrimaryKey = new ITableKey { Name = "primary_key", Type = TableAccessType.standard, IsUnique = false, KeyFields = new List<string>() }, Secondary = new List<ITableKey>() });
        }
        else if (this.IsRAPDerivedEntityName(entityName))
        {
            return VoidType.Get(entityName);
        }
        else if (this.input.Scope.GetDDIC().InErrorNamespace(ddlsName))
        {
            return new UnknownType($"DDLS {ddlsName} not found");
        }
        return VoidType.Get(entityName);
    }

    public AbstractType? ParseType(INode node, string? qualifiedName = null)
    {
        var typeName = node.FindFirstExpression<TypeName>();

        string? text = JsUtils.ToUpper(node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Type>()?.ConcatTokens());
        if (text == null)
        {
            text = JsUtils.ToUpper(node.FindFirstExpression<TypeParam>()?.ConcatTokens());
        }
        if (text == null)
        {
            text = JsUtils.ToUpper(node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.TypeTable>()?.ConcatTokens());
            if (text?.StartsWith("TYPE", StringComparison.Ordinal) == false && text?.StartsWith("LIKE", StringComparison.Ordinal) == false)
            {
                text = "TYPE";
            }
        }
        if (text == null)
        {
            text = JsUtils.ToUpper(node.FindFirstExpression<TypeStructure>()?.ConcatTokens());
        }
        if (text == null)
        {
            text = JsUtils.ToUpper(node.FindFirstExpression<FormParamType>()?.ConcatTokens());
        }
        if (text == null && node.Get() is Parameter && node.FindDirectTokenByText("LIKE") != null)
        {
            text = "LIKE " + JsUtils.Str(typeName?.ConcatTokens());
        }
        if (text == null)
        {
            text = "TYPE";
        }

        AbstractType? found = null;
        if (this.IsRAPTypeStructure(text))
        {
            return VoidType.Get(node.FindFirstExpression<TypeStructure>()?.ConcatTokens());
        }
        else if (text.StartsWith("LIKE LINE OF ", StringComparison.Ordinal))
        {
            string? name = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>()?.ConcatTokens();

            ExpressionNode? e = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Type>();
            if (e == null)
            {
                e = node.FindFirstExpression<FormParamType>();
            }
            if (e == null)
            {
                e = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>();
            }
            if (e == null)
            {
                // old style, eg "LIKE LINE OF tab OCCURS 0 WITH HEADER LINE", it is parsed as TypeName
                e = typeName;
                name = typeName?.ConcatTokens();
            }

            var type = this.ResolveLikeName(e, false);

            AbstractType? row = null;
            if (type == null)
            {
                return new UnknownType("Type error, could not resolve \"" + JsUtils.Str(name) + "\", parseType");
            }
            else if (type is TableType)
            {
                row = ((TableType)type).GetRowType();
            }
            else if (type is VoidType)
            {
                row = ((VoidType)type);
            }
            else
            {
                return new UnknownType("Type error, not a table type " + JsUtils.Str(name));
            }

            if (this.IsOccurs(node))
            {
                return new TableType(row, new ITableOptions { WithHeader = text.Contains(" WITH HEADER LINE", StringComparison.Ordinal), KeyType = TableKeyType.@default }, qualifiedName);
            }
            return row;
        }
        else if (text.StartsWith("LIKE REF TO ", StringComparison.Ordinal))
        {
            var name = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>()?.ConcatTokens();
            var type = this.ResolveLikeName(node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Type>(), false);
            if (type == null)
            {
                return new UnknownType("Type error, could not resolve \"" + JsUtils.Str(name) + "\", parseType");
            }
            return new DataReference(type, name);
        }
        else if (text == "TYPE STANDARD TABLE" || text == "TYPE SORTED TABLE" || text == "TYPE HASHED TABLE" || text == "TYPE INDEX TABLE" || text == "TYPE ANY TABLE")
        {
            return new TableType(AnyType.Get(), new ITableOptions { WithHeader = JsUtils.ToUpper(node.ConcatTokens()).Contains("WITH HEADER LINE", StringComparison.Ordinal), KeyType = TableKeyType.user });
        }
        else if (text.StartsWith("LIKE ", StringComparison.Ordinal))
        {
            ExpressionNode? sub = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Type>();
            if (sub == null)
            {
                sub = node.FindFirstExpression<FormParamType>();
            }
            if (sub == null)
            {
                sub = node.FindFirstExpression<TypeParam>();
            }
            if (sub == null)
            {
                sub = node.FindFirstExpression<TypeName>();
            }
            if (sub == null)
            {
                sub = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>();
            }
            found = this.ResolveLikeName(sub);

            if (found != null && this.IsOccurs(node))
            {
                found = new TableType(found, new ITableOptions { WithHeader = text.Contains("WITH HEADER LINE", StringComparison.Ordinal), KeyType = TableKeyType.@default }, qualifiedName);
            }
            else if (text.EndsWith(" WITH HEADER LINE", StringComparison.Ordinal) && found is TableType)
            {
                found = new TableType(((TableType)found).GetRowType(), new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default }, qualifiedName);
            }
        }
        else if (text.StartsWith("TYPE LINE OF ", StringComparison.Ordinal))
        {
            var sub = node.FindFirstExpression<TypeName>();
            found = this.ResolveTypeName(sub);
            if (((object?)found) is TypedIdentifier)
            {
                found = ((TypedIdentifier)(object)found).GetType();
            }
            if (found is TableType)
            {
                return ((TableType)found).GetRowType();
            }
            else if (found is VoidType)
            {
                return ((VoidType)found);
            }
            else if (found is UnknownType)
            {
                return new UnknownType("TYPE LINE OF, unknown type, " + ((UnknownType)found).GetError());
            }
            else
            {
                return new UnknownType("TYPE LINE OF, unexpected type, " + JsUtils.Str(found?.GetType().Name));
            }
        }
        else if (text.StartsWith("TYPE REF TO ", StringComparison.Ordinal))
        {
            found = this.ResolveTypeRef(typeName);
        }
        else if (text.StartsWith("TYPE", StringComparison.Ordinal))
        {
            found = this.ResolveTypeName(typeName, this.FindLength(node), this.FindDecimals(node), qualifiedName);

            var concat = JsUtils.ToUpper(node.ConcatTokens());
            if (found != null && this.IsOccurs(node))
            {
                found = new TableType(found, new ITableOptions { WithHeader = concat.Contains(" WITH HEADER LINE", StringComparison.Ordinal), KeyType = TableKeyType.@default }, qualifiedName);
            }
            else if (found != null && concat.Contains(" WITH HEADER LINE", StringComparison.Ordinal))
            {
                if (found is VoidType)
                {
                    found = new TableType(((VoidType)found), new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default });
                }
                else if (!((found is TableType)))
                {
                    throw new Exception("WITH HEADER LINE can only be used with internal table");
                }
                else
                {
                    found = new TableType(((TableType)found).GetRowType(), new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default });
                }
            }

            if (found == null && typeName == null)
            {
                int length = 1;

                var len = node.FindDirectExpression<ConstantFieldLength>();
                if (len != null)
                {
                    var @int = len.FindDirectExpression<Integer>();
                    if (@int != null)
                    {
                        length = JsUtils.ParseInt(@int.ConcatTokens(), 10);
                    }
                }
                else if (node.Get() is Parameter)
                {
                    length = this.FindParameterLength(node) ?? length;
                }

                found = new CharacterType(length, new AbstractTypeData { QualifiedName = qualifiedName });
                if (this.IsOccurs(node))
                {
                    found = new TableType(found, new ITableOptions { WithHeader = concat.Contains(" WITH HEADER LINE", StringComparison.Ordinal), KeyType = TableKeyType.@default }, qualifiedName);
                }
            }
        }

        if (text.Contains(" WITH INDICATORS ", StringComparison.Ordinal))
        {
            var componentName = JsUtils.ToUpper(node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Type>()?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentName>()?.ConcatTokens());
            if (componentName == null)
            {
                throw new Exception("parseType, componentName expected");
            }

            if (found is StructureType)
            {
                List<IStructureComponent> newComponents = new List<IStructureComponent>([.. ((StructureType)found).GetComponents()]);
                newComponents.Add(new IStructureComponent { Name = componentName, Type = VoidType.Get("INDICATORStodo") });
                found = new StructureType(newComponents, qualifiedName);
            }
        }

        return found;
    }

    /////////////////////
    private bool IsOccurs(INode node)
    {
        if (node.FindDirectTokenByText("OCCURS") != null)
        {
            return true;
        }
        else if ((node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.TypeTable>()?.FindDirectTokenByText("OCCURS")) != null)
        {
            return true;
        }
        return false;
    }

    private string GetRAPBaseEntityName(string name)
    {
        var association = name.IndexOf("\\_", StringComparison.Ordinal);
        var path = name.IndexOf("\\\\", StringComparison.Ordinal);
        var action = name.IndexOf("~", StringComparison.Ordinal);
        if (association == -1 && path == -1 && action == -1)
        {
            return name;
        }

        var candidates = new List<int> { association, path, action }.Where(i => i != -1).ToList();
        var splitAt = candidates.Min();
        return JsUtils.Substring(name, 0, splitAt);
    }

    /** inside a behavior pool the entities are referenced via the aliases defined in the BDEF */
    private string ResolveRAPAlias(string name)
    {
        return this.input.Scope.FindBehaviorDefinition()?.FindEntityNameByAlias(name) ?? name;
    }

    private bool IsRAPDerivedEntityName(string name)
    {
        return name.Contains("\\", StringComparison.Ordinal) || name.Contains("~", StringComparison.Ordinal);
    }

    private bool IsRAPTableFor(string text)
    {
        return text.StartsWith("TYPE TABLE FOR ", StringComparison.Ordinal);
    }

    private bool IsRAPTypeStructure(string text)
    {
        return text.StartsWith("TYPE STRUCTURE FOR ", StringComparison.Ordinal) || text.StartsWith("TYPE RESPONSE FOR ", StringComparison.Ordinal) || text.StartsWith("TYPE REQUEST FOR ", StringComparison.Ordinal);
    }

    // todo, rewrite this method
    private AbstractType? ResolveTypeChain(ExpressionNode expr)
    {

        var chainText = JsUtils.ToUpper(expr.ConcatTokens());
        if (chainText.Contains("-", StringComparison.Ordinal))
        {
            // workaround for stuff like "sy-repid"
            var built = this.input.Scope.FindType(chainText);
            if (built != null)
            {
                this.input.Scope.AddReference(expr.GetFirstToken(), built, ReferenceType.TypeReference, this.input.Filename);
                return built.GetType();
            }
        }
        else if (chainText.Contains("=>", StringComparison.Ordinal) == false && chainText.Contains("-", StringComparison.Ordinal) == false)
        {
            return null;
        }

        string? className = null;
        string rest = chainText;
        if (chainText.Contains("=>", StringComparison.Ordinal))
        {
            var split = chainText.Split("=>").ToList();
            className = split.ElementAtOrDefault(0);
            rest = split.ElementAtOrDefault(1);
        }
        else if (chainText.Contains("->", StringComparison.Ordinal))
        {
            var split = chainText.Split("->").ToList();
            className = split.ElementAtOrDefault(0);
            rest = split.ElementAtOrDefault(1);
        }
        var subs = rest.Split("-").ToList();
        AbstractType? foundType = null;

        if (!string.IsNullOrEmpty(className) && chainText.Contains("=>", StringComparison.Ordinal))
        {
            var split = chainText.Split("=>").ToList();
            var className2 = split.ElementAtOrDefault(0);

            // the prefix might be itself
            if ((this.input.Scope.GetType() == ScopeType.Interface || this.input.Scope.GetType() == ScopeType.ClassDefinition) && JsUtils.ToUpper(this.input.Scope.GetName()) == JsUtils.ToUpper(className2))
            {
                var foundId = this.input.Scope.FindType(subs.ElementAtOrDefault(0));
                foundType = foundId?.GetType();
                if (foundType == null)
                {
                    return new UnknownType("Could not resolve type " + chainText);
                }
                var token = expr.GetChildren().ElementAtOrDefault(2)?.GetFirstToken();
                if (token != null)
                {
                    this.input.Scope.AddReference(token, foundId, ReferenceType.TypeReference, this.input.Filename);
                }
            }
            else
            {
                // lookup in local and global scope
                var obj = this.input.Scope.FindObjectDefinition(className2);
                if (obj == null && this.input.Scope.GetDDIC().InErrorNamespace(className2) == false)
                {
                    this.input.Scope.AddReference(expr.GetFirstToken(), null, ReferenceType.ObjectOrientedVoidReference, this.input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(className2) });
                    return VoidType.Get(className2);
                }
                else if (obj == null)
                {
                    return new UnknownType("Could not resolve top " + className2 + ", resolveTypeChain");
                }
                string type = obj is global::ScipSharp.Indexer.Abap.Types.ClassDefinition ? "CLAS" : "INTF";

                this.input.Scope.AddReference(expr.GetFirstToken(), ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)obj), ReferenceType.ObjectOrientedReference, this.input.Filename, new IReferenceExtras { OoType = type, OoName = className2 });

                var byName = new ObjectOriented(this.input.Scope).SearchTypeName(obj, subs.ElementAtOrDefault(0));
                foundType = byName?.GetType();
                if (byName == null || foundType == null)
                {
                    return new UnknownType(subs.ElementAtOrDefault(0) + " not found in class or interface");
                }

                var token = expr.GetChildren().ElementAtOrDefault(2)?.GetFirstToken();
                if (token != null)
                {
                    this.input.Scope.AddReference(token, byName, ReferenceType.TypeReference, this.input.Filename);
                }
            }
        }
        else if (!string.IsNullOrEmpty(className) && chainText.Contains("->", StringComparison.Ordinal))
        {
            var varVar = this.input.Scope.FindVariable(className);
            var foo = varVar?.GetType();
            if (foo is ObjectReferenceType)
            {
                var typeName = subs.ElementAtOrDefault(0);
                global::ScipSharp.Indexer.Abap.FileInformation.Identifier id = ((ObjectReferenceType)foo).GetIdentifier();

                if (!((id is global::ScipSharp.Indexer.Abap.Types.ClassDefinition || id is InterfaceDefinition)))
                {
                    var found = this.input.Scope.FindObjectDefinition(((ObjectReferenceType)foo).GetIdentifierName());
                    if (found != null)
                    {
                        id = ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)found);
                    }
                    else
                    {
                        return new UnknownType(((ObjectReferenceType)foo).GetIdentifierName() + " not found in scope");
                    }
                }

                if (id is global::ScipSharp.Indexer.Abap.Types.ClassDefinition || id is InterfaceDefinition)
                {
                    string type = id is global::ScipSharp.Indexer.Abap.Types.ClassDefinition ? "CLAS" : "INTF";
                    this.input.Scope.AddReference(expr.GetFirstToken(), id, ReferenceType.ObjectOrientedReference, this.input.Filename, new IReferenceExtras { OoType = type, OoName = id.GetName() });
                    var byName = new ObjectOriented(this.input.Scope).SearchTypeName((IInterfaceDefinition)id, typeName);
                    foundType = byName?.GetType();
                    if (byName == null || foundType == null)
                    {
                        return new UnknownType(typeName + " not found in class or interface");
                    }
                    var token = expr.GetChildren().ElementAtOrDefault(2)?.GetFirstToken();
                    if (token != null)
                    {
                        this.input.Scope.AddReference(token, byName, ReferenceType.TypeReference, this.input.Filename);
                    }
                }
                else
                {
                    return new UnknownType("Not an object reference, " + className + ", " + id.GetType().Name);
                }
            }
            else if (foo == null)
            {
                return new UnknownType(className + " not found in scope");
            }
            else
            {
                return new UnknownType("Not an object reference, " + className + ", " + foo.GetType().Name);
            }
        }
        else
        {
            var found = this.input.Scope.FindType(subs.ElementAtOrDefault(0));
            foundType = found?.GetType();
            if (foundType == null)
            {
                var typePoolType = this.input.Scope.FindTypePoolType(subs.ElementAtOrDefault(0))?.GetType();
                if (typePoolType != null)
                {
                    //          this.scope.addReference(typeName.getFirstToken(), typePoolType, ReferenceType.TypeReference, this.filename);
                    foundType = typePoolType;
                }

                if (foundType == null)
                {
                    var f = this.input.Scope.GetDDIC().LookupTableOrView(subs.ElementAtOrDefault(0));
                    this.input.Scope.GetDDICReferences().AddUsing(this.input.Scope.GetParentObj(), new IObjectAndToken { Object = f.Object, Filename = this.input.Filename, Token = expr.GetFirstToken() });
                    if (((object)f.Type) is TypedIdentifier)
                    {
                        foundType = ((TypedIdentifier)(object)f.Type).GetType();
                    }
                    else
                    {
                        foundType = f.Type;
                    }
                }
            }
            else
            {
                this.input.Scope.AddReference(expr.GetFirstToken(), found, ReferenceType.TypeReference, this.input.Filename);
            }
            if (foundType == null && this.input.Scope.GetDDIC().InErrorNamespace(subs.ElementAtOrDefault(0)) == false)
            {
                this.input.Scope.AddReference(expr.GetFirstToken(), null, ReferenceType.VoidType, this.input.Filename);
                return VoidType.Get(subs.ElementAtOrDefault(0));
            }
            else if (foundType is VoidType)
            {
                this.input.Scope.AddReference(expr.GetFirstToken(), null, ReferenceType.VoidType, this.input.Filename);
                return ((VoidType)foundType);
            }
            else if (foundType == null)
            {
                return new UnknownType("Unknown type " + subs.ElementAtOrDefault(0));
            }
        }

        JsUtils.Shift(subs);
        while (subs.Count > 0)
        {
            if (foundType is UnknownType || foundType is VoidType)
            {
                return foundType;
            }
            else if (!((foundType is StructureType)))
            {
                return new UnknownType("Not a structured type");
            }
            foundType = ((StructureType)foundType).GetComponentByName(subs.ElementAtOrDefault(0));
            if (foundType == null)
            {
                return new UnknownType($"Field \"{subs.ElementAtOrDefault(0)}\" not found in structure");
            }
            JsUtils.Shift(subs);
        }

        return foundType;
    }

    private string? ResolveConstantValue(ExpressionNode expr)
    {
        // todo: rewrite this method
        if (!((expr.Get() is SimpleFieldChain)))
        {
            throw new Exception("resolveConstantValue");
        }

        var firstNode = expr.GetFirstChild()!;
        var firstToken = firstNode.GetFirstToken();
        var firstName = firstToken.GetStr();
        if (firstNode.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Field)
        {
            var found = this.input.Scope.FindVariable(firstName);
            if (found == null)
            {
                var message = $"\"{firstName}\" not found, findTop";
                this.input.Issues.Add(SyntaxInput.SyntaxIssue(this.input, firstToken, message));
                return null;
            }
            this.input.Scope.AddReference(firstToken, found, ReferenceType.DataReadReference, this.input.Filename);
            if (this.IsConstant(found) == false)
            {
                var message = $"VALUE, {expr.ConcatTokens().ToLowerInvariant()} is not a constant";
                this.input.Issues.Add(SyntaxInput.SyntaxIssue(this.input, firstToken, message));
                return null;
            }
            return this.ConstantValue(expr, found.GetValue());
        }
        else if (firstNode.Get() is ClassName && firstName.ToLowerInvariant() == this.input.Scope.GetName().ToLowerInvariant() && (this.input.Scope.GetType() == ScopeType.Interface || this.input.Scope.GetType() == ScopeType.ClassDefinition))
        {
            var children = expr.GetChildren();
            var token = children.ElementAtOrDefault(2)?.GetFirstToken();
            var found = this.input.Scope.FindVariable(token.GetStr());
            if (found == null)
            {
                var message = $"\"{token.GetStr()}\" not found, findTop";
                this.input.Issues.Add(SyntaxInput.SyntaxIssue(this.input, token, message));
                return null;
            }
            this.input.Scope.AddReference(firstToken, found, ReferenceType.DataReadReference, this.input.Filename);
            if (this.IsConstant(found) == false)
            {
                var message = $"VALUE, {expr.ConcatTokens().ToLowerInvariant()} is not a constant";
                this.input.Issues.Add(SyntaxInput.SyntaxIssue(this.input, token, message));
                return null;
            }
            return this.ConstantValue(expr, found.GetValue());
        }
        else if (firstNode.Get() is ClassName)
        {
            var obj = this.input.Scope.FindObjectDefinition(firstName);
            if (obj == null)
            {
                if (this.input.Scope.ExistsObject(firstName) != null)
                {
                    return null;
                }
                else if (this.input.Scope.GetDDIC().InErrorNamespace(firstName) == true)
                {
                    throw new Exception("resolveConstantValue, not found: " + firstName);
                }
                else
                {
                    this.input.Scope.AddReference(firstNode.GetFirstToken(), null, ReferenceType.ObjectOrientedVoidReference, this.input.Filename, new IReferenceExtras { OoName = JsUtils.ToUpper(firstName) });
                    return null;
                }
            }
            var children = expr.GetChildren();
            var token = children.ElementAtOrDefault(2)?.GetFirstToken();
            var attr = token.GetStr();
            var c = new ObjectOriented(this.input.Scope).SearchConstantName(obj, attr);
            if (c is ClassConstant)
            {
                this.input.Scope.AddReference(firstToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)obj), ReferenceType.ObjectOrientedReference, this.input.Filename, new IReferenceExtras { OoName = obj.GetName() });
                this.input.Scope.AddReference(token, c, ReferenceType.DataReadReference, this.input.Filename);
                return this.ConstantValue(expr, c.GetValue());
            }
            var helper = new ObjectOriented(this.input.Scope);
            var attribute = helper.SearchAttributeName(obj, attr);
            if (attribute != null)
            {
                this.input.Scope.AddReference(firstToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)obj), ReferenceType.ObjectOrientedReference, this.input.Filename, new IReferenceExtras { OoName = obj.GetName() });
                this.input.Scope.AddReference(token, attribute, ReferenceType.DataReadReference, this.input.Filename);
                var message2 = $"VALUE, {expr.ConcatTokens().ToLowerInvariant()} is not a constant";
                this.input.Issues.Add(SyntaxInput.SyntaxIssue(this.input, token, message2));
                return null;
            }
            var message = $"Attribute or constant \"{attr}\" not found in \"{obj.GetName()}\"";
            this.input.Issues.Add(SyntaxInput.SyntaxIssue(this.input, token, message));
            return null;
        }
        else
        {
            throw new Exception("resolveConstantValue, unexpected structure");
        }
    }

    private bool IsConstant(TypedIdentifier identifier)
    {
        if (identifier is ClassAttribute)
        {
            return false;
        }
        var meta = identifier.GetMeta();
        if (meta.Contains(IdentifierMeta.ReadOnly) == false)
        {
            return false;
        }
        if (meta.Contains(IdentifierMeta.Static))
        {
            return true;
        }
        // Built-in constants and built-in system fields share ReadOnly/BuiltIn metadata.
        // Only the constants have a value; system fields such as SY-INDEX do not.
        return meta.Contains(IdentifierMeta.BuiltIn) && identifier.GetValue() != null;
    }

    private string? ConstantValue(ExpressionNode expr, object? value)
    {
        if (value is string str)
        {
            return str;
        }
        else if (value is Dictionary<string, object?> dict)
        {
            object? current = dict;
            foreach (var child in JsUtils.Slice(expr.GetChildren(), 1))
            {
                if (child is ExpressionNode && (((ExpressionNode)child).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentName)
                {
                    if (current is not Dictionary<string, object?> cd)
                    {
                        return null;
                    }
                    current = cd.GetValueOrDefault(((ExpressionNode)child).GetFirstToken().GetStr());
                }
            }
            return current as string;
        }
        return null;
    }

    private AbstractType? ResolveTypeRef(ExpressionNode? chain)
    {
        if (chain == null)
        {
            return null;
        }

        var name = chain.GetFirstToken().GetStr();
        if (chain.GetChildren().Count == 1)
        {
            if (JsUtils.ToUpper(name) == "OBJECT")
            {
                return new GenericObjectReferenceType();
            }
            var search = this.input.Scope.ExistsObject(name);
            if ((search?.id) != null)
            {
                this.input.Scope.AddReference(chain.GetFirstToken(), search.Value.id, ReferenceType.ObjectOrientedReference, this.input.Filename, new IReferenceExtras { OoType = search.Value.ooType, OoName = name });
                return new ObjectReferenceType(search.Value.id, new AbstractTypeData { QualifiedName = name, RTTIName = search.Value.RTTIName });
            }
        }

        var found = this.ResolveTypeName(chain);
        if (found != null && found is VoidType)
        {
            return ((VoidType)found);
        }
        else if (found != null && !((found is UnknownType)))
        {
            return new DataReference(found);
        }
        else if (JsUtils.ToUpper(chain.ConcatTokens()) == "DATA")
        {
            return new DataReference(new DataType());
        }

        if (this.input.Scope.IsBadiDef(name) == true)
        {
            return VoidType.Get(name);
        }

        if (this.input.Scope.GetDDIC()?.InErrorNamespace(name) == false)
        {
            //      this.scope.addReference(chain.getFirstToken(), undefined, ReferenceType.VoidType, this.filename);
            return VoidType.Get(name);
        }

        return new UnknownType("REF, unable to resolve " + name);
    }

    public string? FindValue(INode node)
    {
        var val = node.FindFirstExpression<Value>();
        if (val == null)
        {
            throw new Exception("VALUE missing in expression");
        }

        if (JsUtils.ToUpper(val.ConcatTokens()) == "VALUE IS INITIAL")
        {
            return null;
        }

        var constant = val.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Constant>();
        if (constant != null)
        {
            var conc = val.FindFirstExpression<ConcatenatedConstant>();
            if (conc != null)
            {
                var first = JsUtils.Substring(conc.GetFirstToken().GetStr(), 0, 1);
                string result = "";
                foreach (var token in conc.GetAllTokens())
                {
                    if (token.GetStr() == "&")
                    {
                        continue;
                    }
                    else
                    {
                        result += JsUtils.Substring(token.GetStr(), 1, token.GetStr().Length - 1);
                    }
                }
                return first + result + first;
            }
            else
            {
                return constant.ConcatTokens();
            }
        }

        var chain = val.FindFirstExpression<SimpleFieldChain>();
        if (chain != null)
        {
            // todo, hardcoded for now, should be a generic check that the chain is a constant
            var concat = JsUtils.ToUpper(chain.ConcatTokens());
            if (concat == "SY-DATUM" || concat == "SY-UZEIT")
            {
                var message = "VALUE, " + concat.ToLowerInvariant() + " is not a constant";
                this.input.Issues.Add(SyntaxInput.SyntaxIssue(this.input, chain.GetFirstToken(), message));
                return null;
            }
            return this.ResolveConstantValue(chain);
        }

        throw new Exception("findValue, unexpected");
    }

    private int? FindDecimals(INode node)
    {
        var dec = node.FindDirectExpression<Decimals>()?.FindDirectExpression<Integer>()?.ConcatTokens();
        if (!string.IsNullOrEmpty(dec))
        {
            return JsUtils.ParseInt(dec, 10);
        }

        if (node.Get() is Parameter)
        {
            // PARAMETERS spells it seq("DECIMALS", Source) instead of the Decimals expression
            var source = this.FindParameterAddition(node, "DECIMALS")?.ConcatTokens();
            if (!string.IsNullOrEmpty(source) && Regex.IsMatch(source, @"^\d+$", RegexOptions.ECMAScript))
            {
                return JsUtils.ParseInt(source, 10);
            }
        }

        return null;
    }

    // PARAMETERS spells it seq("LENGTH", Constant) instead of the Length expression
    private int? FindParameterLength(INode node)
    {
        var constant = this.FindParameterAddition(node, "LENGTH");
        if (constant == null)
        {
            return null;
        }
        var length = this.ParseInt(constant.ConcatTokens());
        return length == null || JsUtils.IsNaN(length.Value) ? (int?)null : length.Value;
    }

    // finds the expression following a direct keyword token, "VISIBLE LENGTH" is not "LENGTH"
    private ExpressionNode? FindParameterAddition(INode node, string keyword)
    {
        var children = node.GetChildren();
        for (var i = 0; i < children.Count - 1; i++)
        {
            var child = children.ElementAtOrDefault(i);
            var next = children.ElementAtOrDefault(i + 1);
            if (child is ExpressionNode || JsUtils.ToUpper(((TokenNode)child).GetFirstToken().GetStr()) != keyword || JsUtils.ToUpper(children.ElementAtOrDefault(i - 1)?.GetFirstToken().GetStr()) == "VISIBLE")
            {
                continue;
            }
            return next is ExpressionNode ? ((ExpressionNode)next) : (ExpressionNode?)null;
        }
        return null;
    }

    private int? FindLength(INode node)
    {
        var val = node.FindFirstExpression<Length>();
        var flen = node.FindFirstExpression<ConstantFieldLength>();

        if (val != null && flen != null)
        {
            throw new Exception("Only specify length once");
        }

        if (flen != null)
        {
            var cintExpr = flen.FindFirstExpression<Integer>();
            if (cintExpr != null)
            {
                return this.ParseInt(cintExpr.ConcatTokens());
            }

            var cchain = flen.FindFirstExpression<SimpleFieldChain>();
            if (cchain != null)
            {
                var val2 = this.ResolveConstantValue(cchain);
                return this.ParseInt(val2);
            }
        }

        if (val == null)
        {
            if (node.Get() is Parameter)
            {
                return this.FindParameterLength(node);
            }
            return null;
        }

        var intExpr = val.FindFirstExpression<Integer>();
        if (intExpr != null)
        {
            return this.ParseInt(intExpr.ConcatTokens());
        }

        var strExpr = val.FindFirstExpression<ConstantString>();
        if (strExpr != null)
        {
            return this.ParseInt(strExpr.ConcatTokens());
        }

        var chain = val.FindFirstExpression<SimpleFieldChain>();
        if (chain != null)
        {
            var val2 = this.ResolveConstantValue(chain);
            return this.ParseInt(val2);
        }

        throw new Exception("Unexpected, findLength");
    }

    private int? ParseInt(string? text)
    {
        if (text == null)
        {
            return null;
        }

        if (text.StartsWith("'", StringComparison.Ordinal))
        {
            text = text.Split("'").ToList().ElementAtOrDefault(1);
        }
        else if (text.StartsWith("`", StringComparison.Ordinal))
        {
            text = text.Split("`").ToList().ElementAtOrDefault(1);
        }

        return JsUtils.ParseInt(text, 10);
    }
}
