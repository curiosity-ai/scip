// Ported from abaplint packages/core/src/abap/5_syntax/_syntax_input.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap.Syntax;

// note: these typically doesnt change during the traversal
// so nothing to garbage collect
public class SyntaxInput
{

    public const string CheckSyntaxKey = "check_syntax";

    public static Issue SyntaxIssue(SyntaxInput input, AbstractToken token, string message, IEdit? fix = null)
    {
        return Issue.AtTokenFilename(input.Filename, token, message, SyntaxInput.CheckSyntaxKey, Severity.Error, fix);
    }
    public CurrentScope Scope { get; set; }
    public string Filename { get; set; }
    public List<Issue> Issues { get; set; }
    /** checks which can only run once the full object has been traversed, eg. PERFORM parameters,
     * the FORM might be defined after the PERFORM */
    public List<Action> Deferred { get; set; }
}
