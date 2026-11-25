namespace ScipSharp.Cli.Linting;

/// <summary>A finding reported by <c>scip lint</c>. <see cref="Message"/> starts with <c>error:</c>, <c>warning:</c> or <c>note:</c>.</summary>
public abstract record LintDiagnostic
{
    public abstract string Message { get; }

    public override string ToString() => Message;
}

internal static class RangeFormat
{
    public static string Describe(Range r) => $"{r.Start.Line}:{r.Start.Character}-{r.End.Line}:{r.End.Character}";
}

/// <param name="Symbol">The symbol.</param>
/// <param name="Path">Empty for external symbols.</param>
public sealed record DuplicateSymbolInfoWarning(string Symbol, string Path = "") : LintDiagnostic
{
    public override string Message => Path.Length > 0
        ? $"warning: found repeated SymbolInformation for '{Symbol}' in '{Path}'"
        : $"warning: found repeated SymbolInformation for external symbol '{Symbol}'";
}

public sealed record EmptyStringError(string What, string Context) : LintDiagnostic
{
    public override string Message => $"error: found empty {What} in {Context}";
}

public sealed record NonCanonicalSymbolError(string Original, string Canonical, string Context) : LintDiagnostic
{
    public override string Message => $"error: found non-canonical symbol '{Original}' should be formatted as '{Canonical}'";
}

/// <summary>A symbol string that does not parse.</summary>
public sealed record InvalidSymbolError(string Symbol, string Reason, string Context) : LintDiagnostic
{
    public override string Message => $"error: {Reason}";
}

public sealed record DuplicateDocumentWarning(string Path) : LintDiagnostic
{
    public override string Message => $"warning: found multiple documents with path '{Path}' in index";
}

public sealed record BothLocalAndExternalSymbolError(string Symbol, string Path) : LintDiagnostic
{
    public override string Message =>
        $"error: SymbolInformation for '{Symbol}' is present in both external symbols and document '{Path}'";
}

public sealed record MissingRelationshipFlagError(string Symbol, string Context) : LintDiagnostic
{
    public override string Message =>
        "error: at least one of is_definition, is_reference, is_type_definition or is_implementation should"
        + $" be set for symbol '{Symbol}' in {Context}";
}

public sealed record MissingSymbolInRelationshipError(
    string Symbol, string SymbolContext, string RelatedSymbol, string ExpectedRelatedSymbolContext) : LintDiagnostic
{
    public override string Message =>
        $"error: symbol '{Symbol}' (#1) (in {SymbolContext}) has a relationship to '{RelatedSymbol}' (#2),"
        + $" but couldn't find #2 in {ExpectedRelatedSymbolContext}";
}

public sealed record MultipleRelationshipWarning(string Symbol, string RelatedSymbol) : LintDiagnostic
{
    public override string Message =>
        $"warning: found multiple relationships from '{Symbol}' to '{RelatedSymbol}', which could optimized into a single relationship";
}

public sealed record InvalidOccurrenceRangeError(string Symbol, string Path) : LintDiagnostic
{
    public override string Message =>
        $"error: occurrence for symbol {Symbol} in {Path} has a missing or malformed range";
}

public sealed record MissingSymbolForOccurrenceError(string Symbol, string Path, Range Range) : LintDiagnostic
{
    public override string Message =>
        $"error: found occurrence at {Path} @ {RangeFormat.Describe(Range)} for symbol {Symbol},"
        + " but there is no matching SymbolInformation in external symbols or any document";
}

public sealed record ForwardDefIsDefinitionError(string Symbol, string Path, Range Range) : LintDiagnostic
{
    public override string Message =>
        $"error: forward declaration for {Symbol} at {Path} @ {RangeFormat.Describe(Range)} was marked as definition";
}

public sealed record DuplicateOccurrenceWarning(string Symbol, string Path, Range Range, int SymbolRoles) : LintDiagnostic
{
    public override string Message =>
        $"warning: found duplicate occurrence for {Symbol} at {Path} @ {RangeFormat.Describe(Range)} with role {SymbolRoles}";
}

public sealed record Note(string Text) : LintDiagnostic
{
    public override string Message => $"note: {Text}";
}
