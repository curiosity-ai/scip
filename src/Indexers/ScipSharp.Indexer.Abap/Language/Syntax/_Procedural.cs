// Ported from abaplint packages/core/src/abap/5_syntax/_procedural.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Objects;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax;

public class Procedural
{
    private readonly CurrentScope scope;
    private readonly IRegistry reg;

    public Procedural(IRegistry reg, CurrentScope scope)
    {
        this.scope = scope;
        this.reg = reg;
    }

    public void AddAllFormDefinitions(ABAPFile file, ABAPObject obj, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();
        // an include reached again through a cycle of INCLUDEs is not walked twice
        if (visited.Contains(file.GetFilename()))
        {
            return;
        }
        visited.Add(file.GetFilename());

        var structure = file.GetStructure();
        if (structure != null)
        {
            var input = new SyntaxInput { Scope = CurrentScope.BuildDefault(this.reg, obj), Filename = file.GetFilename(), Issues = new List<Issue>(), Deferred = new List<Action>() };
            foreach (var found in structure.FindAllStructures<global::ScipSharp.Indexer.Abap.Structures.Structures.Form>())
            {
                this.scope.AddFormDefinitions(new List<IFormDefinition> { new global::ScipSharp.Indexer.Abap.Types.FormDefinition(found, input) });
            }
        }

        var stru = file.GetStructure();
        if (stru == null)
        {
            return;
        }

        var includes = stru.FindAllStatements<Include>();
        foreach (var node in includes)
        {
            var found = this.FindInclude(node, obj);
            if (found != null)
            {
                this.AddAllFormDefinitions(found, obj, visited);
            }
        }
    }

    public ABAPFile? FindInclude(StatementNode node, ABAPObject obj)
    {
        // cyclic includes are cut by the callers, includes not found are reported by rule "check_include"
        // todo: how to make sure code is not duplicated here and in rule "check_include" / include graph?
        var expr = node.FindFirstExpression<IncludeName>();
        if (expr == null)
        {
            return null;
        }
        var name = expr.GetFirstToken().GetStr();

        // look in the current function group
        if (obj is FunctionGroup)
        {
            var incl = ((FunctionGroup)obj).GetInclude(name);
            if (incl != null)
            {
                return incl;
            }
        }

        var prog = ((ABAPObject?)(this.reg.GetObject("PROG", name)));
        if (prog != null)
        {
            return prog.GetABAPFiles().ElementAtOrDefault(0);
        }

        // todo, this is slow, try determining the FUGR name from the include name
        foreach (var fugr in this.reg.GetObjectsByType("FUGR"))
        {
            if (fugr is FunctionGroup)
            {
                var found = ((FunctionGroup)fugr).GetInclude(name);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    public void FindFunctionGroupScope(FunctionGroup fg)
    {
        foreach (var module in fg.GetModules())
        {
            if (module.IsGlobalParameters() == false)
            {
                continue;
            }
            // console.dir(fg.getSequencedFiles());
            var fmFile = fg.GetSequencedFiles().FirstOrDefault(f =>
            {
                return f.GetFilename().EndsWith(".fugr." + JsUtils.RegexReplace(module.GetName().ToLowerInvariant(), @"\/", "#", RegexOptions.ECMAScript) + ".abap", StringComparison.Ordinal);
            });
            if (fmFile == null)
            {
                continue;
            }

            var nameToken = fmFile.GetStructure()?.FindFirstStatement<global::ScipSharp.Indexer.Abap.Statements.Statements.FunctionModule>()?.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Field>()?.GetFirstToken();
            if (nameToken == null)
            {
                continue;
            }
            this.AddFunctionScope(module, nameToken, fmFile.GetFilename(), true);
        }
    }

    public void FindFunctionScope(ABAPObject obj, StatementNode node, string filename)
    {
        if (!((obj is FunctionGroup)))
        {
            throw new Exception("findFunctionScope, expected function group input");
        }

        var nameToken = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Field>()!.GetFirstToken();
        var name = nameToken.GetStr();
        this.scope.Push(ScopeType.FunctionModule, name, node.GetFirstToken().GetStart(), filename);

        var definition = ((FunctionGroup)obj).GetModule(name);
        if (definition == null)
        {
            throw new Exception("Function module definition \"" + name + "\" not found");
        }
        if (definition.IsGlobalParameters() == true)
        {
            return;
            // already added at global level
        }

        this.AddFunctionScope(definition, nameToken, filename);
    }

    private void AddFunctionScope(FunctionModuleDefinition definition, AbstractToken nameToken, string filename, bool ignoreIfAlreadyExists = false)
    {
        var ddic = new DDIC(this.reg);

        var allNames = new HashSet<string>();
        foreach (var param in definition.GetParameters())
        {
            AbstractType? found = null;
            if (param.Type == null || param.Type == "")
            {
                found = AnyType.Get();
            }
            else if (param.Type.Contains("=>", StringComparison.Ordinal))
            {
                // then its a type from global INTF or CLAS
                var __a0 = param.Type.Split("=>").ToList();
                var clas = __a0.ElementAtOrDefault(0);
                var name = __a0.ElementAtOrDefault(1);
                var def = this.scope.FindObjectDefinition(clas);
                if (def != null)
                {
                    var type = def.GetTypeDefinitions().GetByName(name);
                    if (type != null)
                    {
                        this.scope.AddReference(nameToken, type, ReferenceType.TypeReference, filename);
                        found = type.GetType();
                    }
                }
            }
            if (found == null)
            {
                found = ddic.Lookup(param.Type!).Type;
            }

            if (param.Direction == FunctionModuleParameterDirection.tables)
            {
                if (found is global::ScipSharp.Indexer.Abap.Types.Basic.TableType)
                {
                    found = new global::ScipSharp.Indexer.Abap.Types.Basic.TableType(((global::ScipSharp.Indexer.Abap.Types.Basic.TableType)found).GetRowType(), new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default });
                }
                else
                {
                    found = new global::ScipSharp.Indexer.Abap.Types.Basic.TableType(found, new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default });
                }
            }

            if ((found is UnknownType || found is VoidType) && (param.Type?.Contains("-", StringComparison.Ordinal)) == true)
            {
                var __a1 = param.Type.Split("-").ToList();
                var name = __a1.ElementAtOrDefault(0);
                var field = __a1.ElementAtOrDefault(1);
                var f = ddic.LookupTableOrView(name).Type;
                if (f != null && f is StructureType)
                {
                    var c = ((StructureType)f).GetComponentByName(field);
                    if (c != null)
                    {
                        found = c;
                    }
                }
                if (found == null || found is UnknownType || found is VoidType)
                {
                    var f2 = this.scope.FindType(name)?.GetType();
                    if (f2 != null && f2 is StructureType)
                    {
                        var c = ((StructureType)f2).GetComponentByName(field);
                        if (c != null)
                        {
                            found = c;
                        }
                    }
                }
            }
            else if ((found is UnknownType || found is VoidType) && (param.Type?.Contains("=>", StringComparison.Ordinal)) == true)
            {
                var __a2 = param.Type.Split("=>").ToList();
                var name = __a2.ElementAtOrDefault(0);
                var field = __a2.ElementAtOrDefault(1);
                var def = this.scope.FindObjectDefinition(name);
                var c = new ObjectOriented(this.scope).SearchTypeName(def, field);
                if (c != null)
                {
                    found = c.GetType();
                }
            }

            if ((found is UnknownType || found is VoidType) && !string.IsNullOrEmpty(param.Type))
            {
                var f = ddic.LookupBuiltinType(param.Type);
                if (f != null)
                {
                    found = f;
                }
                if (found == null || found is UnknownType || found is VoidType)
                {
                    var f2 = this.scope.FindType(param.Type)?.GetType();
                    if (f2 != null)
                    {
                        found = f2;
                    }
                }
                if (found == null || found is UnknownType || found is VoidType)
                {
                    var f2 = this.scope.FindTypePoolType(param.Type)?.GetType();
                    if (f2 != null)
                    {
                        found = f2;
                    }
                }
            }

            if (found is UnknownType && new DDIC(this.reg).InErrorNamespace(param.Type) == false)
            {
                found = VoidType.Get(param.Type);
            }

            if (allNames.Contains(JsUtils.ToUpper(param.Name)))
            {
                // yea, IMPORTING and EXPORTING can have the same name
                // workaround to avoid false postivies, can be improved
                continue;
            }
            else
            {
                var token = new Identifier(nameToken.GetStart(), param.Name);
                var type = new TypedIdentifier(token, filename, found, this.FunctionModuleParameterMeta(param));
                if (ignoreIfAlreadyExists == true)
                {
                    var exists = this.scope.FindVariable(param.Name);
                    if (exists == null)
                    {
                        this.scope.AddNamedIdentifier(param.Name, type);
                    }
                }
                else
                {
                    this.scope.AddNamedIdentifier(param.Name, type);
                }
                allNames.Add(JsUtils.ToUpper(param.Name));
            }
        }
    }

    private List<string> FunctionModuleParameterMeta(IFunctionModuleParameter param)
    {
        List<string> ret = new List<string>();
        if (param.Direction == FunctionModuleParameterDirection.importing)
        {
            ret.Add(IdentifierMeta.FunctionModuleImporting);
        }
        else if (param.Direction == FunctionModuleParameterDirection.exporting)
        {
            ret.Add(IdentifierMeta.FunctionModuleExporting);
        }
        else if (param.Direction == FunctionModuleParameterDirection.changing)
        {
            ret.Add(IdentifierMeta.FunctionModuleChanging);
        }
        else if (param.Direction == FunctionModuleParameterDirection.tables)
        {
            ret.Add(IdentifierMeta.FunctionModuleTables);
        }
        if (param.PassByValue)
        {
            ret.Add(IdentifierMeta.PassByValue);
        }
        return ret;
    }
}
