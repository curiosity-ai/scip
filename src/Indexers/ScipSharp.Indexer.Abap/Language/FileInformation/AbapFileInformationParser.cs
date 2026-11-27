// Ported from abaplint packages/core/src/abap/4_file_information/abap_file_information_parser.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.FileInformation;

using Expressions = ScipSharp.Indexer.Abap.Statements.Expressions;
using Statements = ScipSharp.Indexer.Abap.Statements.Statements;
using Structures = ScipSharp.Indexer.Abap.Structures.Structures;
using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ABAPFileInformationParser
{
    private readonly string filename;

    public ABAPFileInformationParser(string filename)
    {
        this.filename = filename;
    }

    public ParsedFileInformation Parse(StructureNode? structure)
    {
        var result = new ParsedFileInformation();

        if (structure == null)
        {
            return result;
        }

        result.Classes = ParseClasses(structure);
        result.Interfaces = ParseInterfaces(structure);
        result.Implementations = ParseClassImplementations(structure);
        result.Forms = ParseForms(structure);

        return result;
    }

    private List<InfoClassImplementation> ParseClassImplementations(StructureNode structure)
    {
        var implementations = new List<InfoClassImplementation>();

        foreach (var found in structure.FindAllStructures<Structures.ClassImplementation>())
        {
            var methods = new List<Identifier>();
            foreach (var method in found.FindAllStructures<Structures.Method>())
            {
                var methodName = method.FindFirstExpression<Expressions.MethodName>()?.GetFirstToken();
                if (methodName != null)
                {
                    methods.Add(new Identifier(methodName, filename));
                }
            }

            var name = found.FindFirstStatement<Statements.ClassImplementation>()!.FindFirstExpression<Expressions.ClassName>()!.GetFirstToken();
            implementations.Add(new InfoClassImplementation
            {
                Name = name.GetStr(),
                Identifier = new Identifier(name, filename),
                Methods = methods,
            });
        }

        return implementations;
    }

    private List<InfoFormDefinition> ParseForms(StructureNode structure)
    {
        var forms = new List<InfoFormDefinition>();

        foreach (var statement in structure.FindAllStructures<Structures.Form>())
        {
            // FORMs can contain a dash in the name
            var pos = statement.FindFirstExpression<Expressions.FormName>()!.GetFirstToken().GetStart();
            var name = statement.FindFirstExpression<Expressions.FormName>()!.ConcatTokens();
            var nameToken = new Tokens.Identifier(pos, name);
            forms.Add(new InfoFormDefinition
            {
                Name = nameToken.GetStr(),
                Identifier = new Identifier(nameToken, filename),
            });
        }

        return forms;
    }

    private List<InfoInterfaceDefinition> ParseInterfaces(StructureNode structure)
    {
        var interfaces = new List<InfoInterfaceDefinition>();

        foreach (var found in structure.FindDirectStructures<Structures.Interface>())
        {
            var i = found.FindFirstStatement<Statements.Interface>();
            if (i == null)
            {
                throw new InvalidOperationException("Interface expected, parseInterfaces");
            }
            var interfaceName = i.FindDirectExpression<Expressions.InterfaceName>()!.GetFirstToken();
            var methods = ParseMethodDefinition(found, Visibility.Public);
            var attributes = ParseAttributes(found, Visibility.Public);
            var aliases = ParseAliases(found, Visibility.Public);
            var constants = ParseConstants(found, Visibility.Public);
            var g = i.FindDirectExpression<Expressions.ClassGlobal>();

            interfaces.Add(new InfoInterfaceDefinition
            {
                Name = interfaceName.GetStr(),
                Identifier = new Identifier(interfaceName, filename),
                IsLocal = g == null,
                IsGlobal = g != null,
                Interfaces = GetImplementing(found),
                Aliases = aliases,
                Methods = methods,
                Constants = constants,
                Attributes = attributes,
            });
        }

        return interfaces;
    }

    private List<InfoClassDefinition> ParseClasses(StructureNode structure)
    {
        var classes = new List<InfoClassDefinition>();

        foreach (var found in structure.FindAllStructures<Structures.ClassDefinition>())
        {
            var className = found.FindFirstStatement<Statements.ClassDefinition>()!.FindFirstExpression<Expressions.ClassName>()!.GetFirstToken();

            var publicSection = found.FindFirstStructure<Structures.PublicSection>();
            var protectedSection = found.FindFirstStructure<Structures.ProtectedSection>();
            var privateSection = found.FindFirstStructure<Structures.PrivateSection>();

            var methods = ParseMethodDefinition(publicSection, Visibility.Public);
            methods.AddRange(ParseMethodDefinition(protectedSection, Visibility.Protected));
            methods.AddRange(ParseMethodDefinition(privateSection, Visibility.Private));

            var attributes = ParseAttributes(publicSection, Visibility.Public);
            attributes.AddRange(ParseAttributes(protectedSection, Visibility.Protected));
            attributes.AddRange(ParseAttributes(privateSection, Visibility.Private));

            var aliases = ParseAliases(publicSection, Visibility.Public);
            aliases.AddRange(ParseAliases(protectedSection, Visibility.Protected));
            aliases.AddRange(ParseAliases(privateSection, Visibility.Private));

            var constants = ParseConstants(publicSection, Visibility.Public);
            constants.AddRange(ParseConstants(protectedSection, Visibility.Protected));
            constants.AddRange(ParseConstants(privateSection, Visibility.Private));

            var superClassName = found.FindFirstExpression<Expressions.SuperClassName>()?.GetFirstToken().GetStr();
            var containsGlobal = found.FindFirstExpression<Expressions.ClassGlobal>();
            var cdef = found.FindFirstStatement<Statements.ClassDefinition>();
            var concat = cdef != null ? JsString.ToUpper(cdef.ConcatTokens()) : "";

            RiskLevel? riskLevel = null;
            if (concat.Contains("RISK LEVEL CRITICAL", StringComparison.Ordinal))
            {
                riskLevel = RiskLevel.critical;
            }
            else if (concat.Contains("RISK LEVEL DANGEROUS", StringComparison.Ordinal))
            {
                riskLevel = RiskLevel.dangerous;
            }
            else if (concat.Contains("RISK LEVEL HARMLESS", StringComparison.Ordinal))
            {
                riskLevel = RiskLevel.harmless;
            }

            Duration? duration = null;
            if (concat.Contains("DURATION SHORT", StringComparison.Ordinal))
            {
                duration = Duration.@short;
            }
            else if (concat.Contains("DURATION LONG", StringComparison.Ordinal))
            {
                duration = Duration.@long;
            }
            else if (concat.Contains("DURATION MEDIUM", StringComparison.Ordinal))
            {
                duration = Duration.medium;
            }

            classes.Add(new InfoClassDefinition
            {
                Name = className.GetStr(),
                Identifier = new Identifier(className, filename),
                IsLocal = containsGlobal == null,
                IsGlobal = containsGlobal != null,
                Methods = methods,
                SuperClassName = superClassName,
                Interfaces = GetImplementing(found),
                IsForTesting = concat.Contains(" FOR TESTING", StringComparison.Ordinal),
                Duration = duration,
                RiskLevel = riskLevel,
                IsAbstract = cdef?.FindDirectTokenByText("ABSTRACT") != null,
                IsSharedMemory = concat.Contains(" SHARED MEMORY ENABLED", StringComparison.Ordinal),
                BehaviorDefinitionName = FindBehaviorDefinitionName(cdef),
                IsFinal = found.FindFirstExpression<Expressions.ClassFinal>() != null,
                Aliases = aliases,
                Attributes = attributes,
                Constants = constants,
            });
        }

        return classes;
    }

    ///////////////////

    /// <summary>"FOR BEHAVIOR OF" and "FOR EVENTS OF" both use the BehaviorDefinitionName expression,
    /// so look at the token two positions before the expression to tell them apart.</summary>
    private static string? FindBehaviorDefinitionName(StatementNode? cdef)
    {
        var children = cdef?.GetChildren() ?? [];
        for (var i = 2; i < children.Count; i++)
        {
            var child = children[i];
            if (child is ExpressionNode en
                && en.Get() is Expressions.BehaviorDefinitionName
                && JsString.ToUpper(children[i - 2].ConcatTokens()) == "BEHAVIOR")
            {
                return en.ConcatTokens();
            }
        }
        return null;
    }

    private static List<InfoImplementing> GetImplementing(StructureNode input)
    {
        var ret = new List<InfoImplementing>();
        foreach (var node in input.FindAllStatements<Statements.InterfaceDef>())
        {
            var @abstract = node.FindDirectExpression<Expressions.AbstractMethods>();
            var abstractMethods = new List<string>();
            if (@abstract != null)
            {
                foreach (var m in @abstract.FindDirectExpressions<Expressions.MethodName>())
                {
                    abstractMethods.Add(JsString.ToUpper(m.ConcatTokens()));
                }
            }

            var final = node.FindDirectExpression<Expressions.FinalMethods>();
            var finalMethods = new List<string>();
            if (final != null)
            {
                foreach (var m in final.FindDirectExpressions<Expressions.MethodName>())
                {
                    finalMethods.Add(JsString.ToUpper(m.ConcatTokens()));
                }
            }

            var concat = JsString.ToUpper(node.ConcatTokens());
            var allAbstract = concat.Contains(" ALL METHODS ABSTRACT", StringComparison.Ordinal);
            var partial = concat.Contains(" PARTIALLY IMPLEMENTED", StringComparison.Ordinal);
            var name = JsString.ToUpper(node.FindFirstExpression<Expressions.InterfaceName>()!.GetFirstToken().GetStr());
            ret.Add(new InfoImplementing
            {
                Name = name,
                Partial = partial,
                AllAbstract = allAbstract,
                AbstractMethods = abstractMethods,
                FinalMethods = finalMethods,
            });
        }
        return ret;
    }

    private List<InfoAlias> ParseAliases(StructureNode? node, Visibility visibility)
    {
        if (node == null)
        {
            return [];
        }

        var ret = new List<InfoAlias>();
        foreach (var a in node.FindAllStatements<Statements.Aliases>())
        {
            var name = a.FindFirstExpression<Expressions.SimpleName>()!.GetFirstToken();
            var comp = a.FindFirstExpression<Expressions.Field>()!.GetFirstToken();

            ret.Add(new InfoAlias
            {
                Name = name.GetStr(),
                Identifier = new Identifier(name, filename),
                Visibility = visibility,
                Component = comp.GetStr(),
            });
        }

        return ret;
    }

    private List<InfoConstant> ParseConstants(StructureNode? node, Visibility visibility)
    {
        if (node == null)
        {
            return [];
        }

        var results = new List<InfoConstant>();
        foreach (var constant in node.FindAllStatements<Statements.Constant>())
        {
            var name = constant.FindFirstExpression<Expressions.DefinitionName>()!.GetFirstToken();
            var typeName = constant.FindFirstExpression<Expressions.TypeName>();

            // VALUE `const_value` -> `const_value`
            var literal = constant.FindFirstExpression<Expressions.Value>()?.GetTokens()[1].GetStr() ?? "``";
            // `const_value` -> const_value, JS slice(1, length - 1)
            var value = literal.Length - 1 > 1 ? literal.Substring(1, literal.Length - 2) : "";

            results.Add(new InfoConstant
            {
                Name = name.GetStr(),
                TypeName = typeName != null ? typeName.GetFirstToken().GetStr() : "",
                Value = value,
                Identifier = new Identifier(name, filename),
                Visibility = visibility,
            });
        }

        return results;
    }

    private List<InfoAttribute> ParseAttributes(StructureNode? node, Visibility visibility)
    {
        if (node == null)
        {
            return [];
        }

        var contents = node.FindFirstStructure<Structures.SectionContents>();
        if (contents == null)
        {
            return [];
        }

        var ret = new List<InfoAttribute>();
        foreach (var d in contents.FindDirectStatements<Statements.Data>())
        {
            var name = d.FindFirstExpression<Expressions.DefinitionName>()!.GetFirstToken();
            ret.Add(new InfoAttribute
            {
                Name = name.GetStr(),
                Identifier = new Identifier(name, filename),
                Level = AttributeLevel.Instance,
                ReadOnly = JsString.ToUpper(d.ConcatTokens()).Contains(" READ-ONLY", StringComparison.Ordinal),
                Visibility = visibility,
            });
        }
        foreach (var d in contents.FindDirectStatements<Statements.ClassData>())
        {
            var name = d.FindFirstExpression<Expressions.DefinitionName>()!.GetFirstToken();
            ret.Add(new InfoAttribute
            {
                Name = name.GetStr(),
                Identifier = new Identifier(name, filename),
                Level = AttributeLevel.Static,
                ReadOnly = JsString.ToUpper(d.ConcatTokens()).Contains(" READ-ONLY", StringComparison.Ordinal),
                Visibility = visibility,
            });
        }
        foreach (var d in contents.FindDirectStatements<Statements.Constant>())
        {
            var name = d.FindFirstExpression<Expressions.DefinitionName>()!.GetFirstToken();
            ret.Add(new InfoAttribute
            {
                Name = name.GetStr(),
                Identifier = new Identifier(name, filename),
                Level = AttributeLevel.Constant,
                ReadOnly = true,
                Visibility = visibility,
            });
        }

        return ret;
    }

    private List<InfoMethodDefinition> ParseMethodDefinition(StructureNode? node, Visibility visibility)
    {
        if (node == null)
        {
            return [];
        }

        var methods = new List<InfoMethodDefinition>();
        foreach (var def in node.FindAllStatements<Statements.MethodDef>())
        {
            var methodName = def.FindDirectExpression<Expressions.MethodName>()?.GetFirstToken();
            if (methodName == null)
            {
                continue;
            }

            var parameters = ParseMethodParameters(def);
            var concatenated = JsString.ToUpper(def.ConcatTokens());

            methods.Add(new InfoMethodDefinition
            {
                Name = methodName.GetStr(),
                Identifier = new Identifier(methodName, filename),
                IsRedefinition = def.FindDirectExpression<Expressions.Redefinition>() != null,
                IsForTesting = concatenated.Contains(" FOR TESTING", StringComparison.Ordinal),
                IsFinal = concatenated.Contains(" FINAL", StringComparison.Ordinal),
                IsDefaultIgnore = concatenated.Contains(" DEFAULT IGNORE", StringComparison.Ordinal),
                IsDefaultFail = concatenated.Contains(" DEFAULT FAIL", StringComparison.Ordinal),
                IsAbstract = def.FindDirectExpression<Expressions.Abstract>() != null,
                IsEventHandler = def.FindDirectExpression<Expressions.EventHandler>() != null,
                Visibility = visibility,
                Parameters = parameters,
                Exceptions = [], // todo
            });
        }
        return methods;
    }

    /// <summary>JS string.replace("!", ""): removes the first occurrence only.</summary>
    private static string RemoveFirstBang(string s)
    {
        var idx = s.IndexOf('!');
        return idx < 0 ? s : s.Remove(idx, 1);
    }

    // todo, refactor this method, it is too long
    private List<InfoMethodParameter> ParseMethodParameters(StatementNode node)
    {
        var ret = new List<InfoMethodParameter>();

        var importing = node.FindFirstExpression<Expressions.MethodDefImporting>();
        if (importing != null)
        {
            foreach (var param in importing.FindAllExpressions<Expressions.MethodParam>())
            {
                var name = param.FindDirectExpression<Expressions.MethodParamName>()?.GetFirstToken();
                if (name != null)
                {
                    ret.Add(new InfoMethodParameter
                    {
                        Name = RemoveFirstBang(name.GetStr()),
                        Identifier = new Identifier(name, filename),
                        Direction = MethodParameterDirection.Importing,
                    });
                }
            }
        }

        var exporting = node.FindFirstExpression<Expressions.MethodDefExporting>();
        if (exporting != null)
        {
            foreach (var param in exporting.FindAllExpressions<Expressions.MethodParam>())
            {
                var name = param.FindDirectExpression<Expressions.MethodParamName>()?.GetFirstToken();
                if (name != null)
                {
                    ret.Add(new InfoMethodParameter
                    {
                        Name = RemoveFirstBang(name.GetStr()),
                        Identifier = new Identifier(name, filename),
                        Direction = MethodParameterDirection.Exporting,
                    });
                }
            }
        }

        var changing = node.FindFirstExpression<Expressions.MethodDefChanging>();
        if (changing != null)
        {
            foreach (var param in changing.FindAllExpressions<Expressions.MethodParam>())
            {
                var name = param.FindDirectExpression<Expressions.MethodParamName>()?.GetFirstToken();
                if (name != null)
                {
                    ret.Add(new InfoMethodParameter
                    {
                        Name = RemoveFirstBang(name.GetStr()),
                        Identifier = new Identifier(name, filename),
                        Direction = MethodParameterDirection.Changing,
                    });
                }
            }
        }

        var returning = node.FindFirstExpression<Expressions.MethodDefReturning>();
        if (returning != null)
        {
            var name = returning.FindDirectExpression<Expressions.MethodParamName>()?.GetFirstToken();
            if (name != null)
            {
                ret.Add(new InfoMethodParameter
                {
                    Name = RemoveFirstBang(name.GetStr()),
                    Identifier = new Identifier(name, filename),
                    Direction = MethodParameterDirection.Returning,
                });
            }
        }

        return ret;
    }
}
