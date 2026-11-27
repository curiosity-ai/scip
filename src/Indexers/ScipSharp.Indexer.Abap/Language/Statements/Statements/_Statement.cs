// Ported from abaplint packages/core/src/abap/2_statements/statements/_statement.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Statements.Statements;

public interface IStatement
{
    IStatementRunnable GetMatcher();
}

/// <summary>Marker for the pseudo statements below, they have no matcher and are not part of the statement map.</summary>
public interface IPseudoStatement : IStatement
{
}

public sealed class Unknown : IPseudoStatement
{
    public IStatementRunnable GetMatcher() => throw new InvalidOperationException("Unknown Statement, get_matcher");
}

public sealed class Comment : IPseudoStatement
{
    public IStatementRunnable GetMatcher() => throw new InvalidOperationException("Comment Statement, get_matcher");
}

public sealed class Empty : IPseudoStatement
{
    public IStatementRunnable GetMatcher() => throw new InvalidOperationException("Empty Statement, get_matcher");
}

public sealed class MacroCall : IPseudoStatement
{
    public IStatementRunnable GetMatcher() => throw new InvalidOperationException("MacroCall Statement, get_matcher");
}

public sealed class MacroContent : IPseudoStatement
{
    public IStatementRunnable GetMatcher() => throw new InvalidOperationException("MacroContent Statement, get_matcher");
}

public sealed class MacroRecursion : IPseudoStatement
{
    public IStatementRunnable GetMatcher() => throw new InvalidOperationException("MacroRecursion Statement, get_matcher");
}

public sealed class NativeSQL : IPseudoStatement
{
    public IStatementRunnable GetMatcher() => throw new InvalidOperationException("NativeSQL Statement, get_matcher");
}
