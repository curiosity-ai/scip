// C# addition, not a port of an upstream file. The rules (packages/core/src/rules, rules_runner.ts) are not
// ported; Registry.FindIssues() reports what the parser and the check_syntax rule (rules/check_syntax.ts) report.
using ScipSharp.Indexer.Abap.Syntax;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap;

public static class SyntaxRulesRunner
{
    public static List<Issue> Run(IRegistry reg, IEnumerable<IObject> objects)
    {
        var issues = new List<Issue>();
        foreach (var obj in objects.ToList())
        {
            if (reg.IsDependency(obj))
            {
                continue;
            }
            issues.AddRange(obj.GetParsingIssues());
            // rules/check_syntax.ts
            if (obj is ABAPObject abap)
            {
                issues.AddRange(new SyntaxLogic(reg, abap).Run().Issues);
            }
        }
        return issues;
    }
}
