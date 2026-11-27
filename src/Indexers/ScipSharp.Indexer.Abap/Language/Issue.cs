// Ported from abaplint packages/core/src/issue.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap;

using Identifier = ScipSharp.Indexer.Abap.FileInformation.Identifier;

public sealed record Fix(string Description, IEdit Edit);

public sealed class IIssueData
{
    public required string Filename { get; init; }
    public required string Message { get; init; }
    public required string Key { get; init; }
    public required Position Start { get; init; }
    public required Position End { get; init; }
    public required Severity Severity { get; init; }
    /// <summary>The default fix for the issue, it always makes sense to apply this fix.</summary>
    public IEdit? DefaultFix { get; set; }
    /// <summary>Alternative quick fixes, the developer must choose which to apply.</summary>
    public IReadOnlyList<Fix>? AlternativeFixes { get; init; }
}

public sealed class Issue
{
    private readonly IIssueData data;

    public static Issue AtRow(IFile file, int row, string message, string key, Severity? severity = null)
    {
        var start = new Position(row, 1);
        var end = new Position(row, file.GetRawRows()[row - 1].Length + 1);
        return new Issue(new IIssueData { Filename = file.GetFilename(), Message = message, Key = key, Start = start, End = end, Severity = severity ?? Severity.Error });
    }

    public static Issue AtStatement(IFile file, StatementNode statement, string message, string key,
                                    Severity? severity = null, IEdit? fix = null, IReadOnlyList<Fix>? alternativeFixes = null)
    {
        return AtRange(file, statement.GetStart(), statement.GetEnd(), message, key, severity, fix, alternativeFixes);
    }

    public static Issue AtPosition(IFile file, Position start, string message, string key, Severity? severity = null, IEdit? fix = null)
    {
        var row = start.GetRow();
        var end = new Position(row, file.GetRawRows()[row - 1].Length + 1);
        return new Issue(new IIssueData { Filename = file.GetFilename(), Message = message, Key = key, Start = start, End = end, DefaultFix = fix, Severity = severity ?? Severity.Error });
    }

    public static Issue AtRowRange(IFile file, int row, int startCol, int endCol, string message, string key, Severity? severity = null, IEdit? fix = null)
    {
        var start = new Position(row, startCol);
        var end = new Position(row, endCol);
        return new Issue(new IIssueData { Filename = file.GetFilename(), Message = message, Key = key, Start = start, End = end, DefaultFix = fix, Severity = severity ?? Severity.Error });
    }

    public static Issue AtRange(IFile file, Position start, Position end, string message, string key,
                                Severity? severity = null, IEdit? fix = null, IReadOnlyList<Fix>? alternativeFixes = null)
    {
        return new Issue(new IIssueData { Filename = file.GetFilename(), Message = message, Key = key, Start = start, End = end, DefaultFix = fix, Severity = severity ?? Severity.Error, AlternativeFixes = alternativeFixes });
    }

    public static Issue AtToken(IFile file, AbstractToken token, string message, string key, Severity? severity = null, IEdit? fix = null)
    {
        return new Issue(new IIssueData { Filename = file.GetFilename(), Message = message, Key = key, Start = token.GetStart(), End = token.GetEnd(), Severity = severity ?? Severity.Error, DefaultFix = fix });
    }

    public static Issue AtTokenFilename(string filename, AbstractToken token, string message, string key, Severity? severity = null, IEdit? fix = null)
    {
        return new Issue(new IIssueData { Filename = filename, Message = message, Key = key, Start = token.GetStart(), End = token.GetEnd(), Severity = severity ?? Severity.Error, DefaultFix = fix });
    }

    public static Issue AtIdentifier(Identifier identifier, string message, string key, Severity? severity = null, IEdit? fix = null)
    {
        return new Issue(new IIssueData { Filename = identifier.GetFilename(), Message = message, Key = key, Start = identifier.GetStart(), End = identifier.GetEnd(), Severity = severity ?? Severity.Error, DefaultFix = fix });
    }

    public Issue(IIssueData data)
    {
        this.data = data;

        if (this.data.Start is VirtualPosition)
        {
            // no quick fixes inside macros
            this.data.DefaultFix = null;
        }

        if (this.data.Start.GetCol() < 1)
        {
            throw new InvalidOperationException("issue, start col < 1");
        }
        else if (this.data.End.GetCol() < 1)
        {
            throw new InvalidOperationException("issue, end col < 1");
        }
    }

    public IIssueData GetData() => data;
    public string GetMessage() => data.Message;
    public string GetKey() => data.Key;
    public Position GetStart() => data.Start;
    public Position GetEnd() => data.End;
    public string GetFilename() => data.Filename;
    public IEdit? GetDefaultFix() => data.DefaultFix;
    public IReadOnlyList<Fix>? GetAlternativeFixes() => data.AlternativeFixes;
    public Severity GetSeverity() => data.Severity;
}
