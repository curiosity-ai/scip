// Ported from abaplint packages/core/src/abap/5_syntax/expressions/_check_database_fields.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Objects;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public static class CheckDatabaseFieldsHelper
{

    public static void CheckDatabaseFields(List<string> fields, List<IParseTypeObject?> dbSources, SyntaxInput input, AbstractToken token)
    {
        if (dbSources.Count > 1)
        {
            return;
        }

        var first = dbSources.ElementAtOrDefault(0);
        if (first == null)
        {
            // then its voided
            return;
        }

        var type = first.ParseType(input.Scope.GetRegistry());
        if (type is VoidType || type is UnknownType)
        {
            return;
        }
        if (!((type is StructureType)))
        {
            var message = "checkFields, expected structure, " + type.GetType().Name;
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, token, message));
            return;
        }

        foreach (var field in fields)
        {
            if (field == "*")
            {
                continue;
            }

            if (Regex.IsMatch(field, @"^[A-Z_]\w*$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase) && ((StructureType)type).GetComponentByName(field) == null)
            {
                var message = $"checkFields, field {field} not found";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, token, message));
                return;
            }
        }
    }
}
