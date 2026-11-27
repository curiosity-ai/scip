// Ported from abaplint packages/core/src/abap/4_file_information/_abap_file_information.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
// Only helper functions to get data from single file, no type information.
//
// The TS string enums (AttributeLevel, MethodParameterDirection, Duration, RiskLevel) are C# enums;
// FileInformationEnums.ToTsString() gives the TS string value.
namespace ScipSharp.Indexer.Abap.FileInformation;

public enum AttributeLevel
{
    Instance,
    Static,
    Constant,
}

public enum MethodParameterDirection
{
    Importing,
    Exporting,
    Changing,
    Returning,
}

public enum Duration
{
    @short,
    medium,
    @long,
}

public enum RiskLevel
{
    harmless,
    critical,
    dangerous,
}

public static class FileInformationEnums
{
    /// <summary>"instance", "static", "constant".</summary>
    public static string ToTsString(this AttributeLevel level) => level switch
    {
        AttributeLevel.Instance => "instance",
        AttributeLevel.Static => "static",
        AttributeLevel.Constant => "constant",
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    /// <summary>"importing", "exporting", "changing", "returning".</summary>
    public static string ToTsString(this MethodParameterDirection direction) => direction switch
    {
        MethodParameterDirection.Importing => "importing",
        MethodParameterDirection.Exporting => "exporting",
        MethodParameterDirection.Changing => "changing",
        MethodParameterDirection.Returning => "returning",
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    /// <summary>"SHORT", "MEDIUM", "LONG".</summary>
    public static string ToTsString(this Duration duration) => duration switch
    {
        Duration.@short => "SHORT",
        Duration.medium => "MEDIUM",
        Duration.@long => "LONG",
        _ => throw new ArgumentOutOfRangeException(nameof(duration)),
    };

    /// <summary>"HARMLESS", "CRITICAL", "DANGEROUS".</summary>
    public static string ToTsString(this RiskLevel riskLevel) => riskLevel switch
    {
        RiskLevel.harmless => "HARMLESS",
        RiskLevel.critical => "CRITICAL",
        RiskLevel.dangerous => "DANGEROUS",
        _ => throw new ArgumentOutOfRangeException(nameof(riskLevel)),
    };
}

public class InfoAttribute
{
    public required string Name { get; init; }
    public required Identifier Identifier { get; init; }
    public required AttributeLevel Level { get; init; }
    public required bool ReadOnly { get; init; }
    public required Visibility Visibility { get; init; }
}

public class InfoMethodParameter
{
    public required string Name { get; init; }
    public required Identifier Identifier { get; init; }
    public required MethodParameterDirection Direction { get; init; }
}

public class InfoMethodDefinition
{
    public required string Name { get; init; }
    public required Identifier Identifier { get; init; }
    // todo, level, Instance or Static
    public required bool IsRedefinition { get; init; }
    public required bool IsEventHandler { get; init; }
    public required bool IsForTesting { get; init; }
    public required bool IsAbstract { get; init; }
    public required bool IsFinal { get; init; }
    /// <summary>"DEFAULT IGNORE", 7.40 SP08: an implementing class need not implement this method,
    /// and a call of it on an unimplemented method does nothing.</summary>
    public required bool IsDefaultIgnore { get; init; }
    /// <summary>"DEFAULT FAIL", 7.40 SP08: an implementing class need not implement this method,
    /// and a call of it raises CX_SY_DYN_CALL_ILLEGAL_METHOD.</summary>
    public required bool IsDefaultFail { get; init; }
    public required Visibility Visibility { get; init; }
    public required List<InfoMethodParameter> Parameters { get; init; }
    public required List<string> Exceptions { get; init; }
}

public class InfoInterfaceDefinition
{
    public required string Name { get; init; }
    public required Identifier Identifier { get; init; }
    public required bool IsLocal { get; init; }
    public required bool IsGlobal { get; init; }
    public required IReadOnlyList<InfoImplementing> Interfaces { get; init; }
    public required IReadOnlyList<InfoMethodDefinition> Methods { get; init; }
    public required IReadOnlyList<InfoAlias> Aliases { get; init; }
    public required IReadOnlyList<InfoConstant> Constants { get; init; }
    public required IReadOnlyList<InfoAttribute> Attributes { get; init; }
}

public class InfoConstant
{
    public required Identifier Identifier { get; init; }
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public required Visibility Visibility { get; init; }
    public required string Value { get; init; }
}

public class InfoAlias
{
    public required string Name { get; init; }
    public required Identifier Identifier { get; init; }
    public required Visibility Visibility { get; init; }
    public required string Component { get; init; }
}

public class InfoImplementing
{
    public required string Name { get; init; }
    public required bool Partial { get; init; }
    public required bool AllAbstract { get; init; }
    public required List<string> AbstractMethods { get; init; }
    public required List<string> FinalMethods { get; init; }
}

public class InfoClassDefinition : InfoInterfaceDefinition
{
    public required string? SuperClassName { get; init; }
    public required bool IsAbstract { get; init; }
    public required bool IsFinal { get; init; }
    public required bool IsForTesting { get; init; }
    public required Duration? Duration { get; init; }
    public required RiskLevel? RiskLevel { get; init; }
    public required bool IsSharedMemory { get; init; }
    /// <summary>Name of the BDEF from "FOR BEHAVIOR OF", null if not a behavior pool.</summary>
    public required string? BehaviorDefinitionName { get; init; }
}

public class InfoClassImplementation
{
    public required string Name { get; init; }
    public required Identifier Identifier { get; init; }
    public required IReadOnlyList<Identifier> Methods { get; init; }
}

public class InfoFormDefinition
{
    public required string Name { get; init; }
    public required Identifier Identifier { get; init; }
}

public interface IABAPFileInformation
{
    IReadOnlyList<InfoInterfaceDefinition> ListInterfaceDefinitions();
    InfoInterfaceDefinition? GetInterfaceDefinitionByName(string name);

    IReadOnlyList<InfoClassDefinition> ListClassDefinitions();
    InfoClassDefinition? GetClassDefinitionByName(string name);

    IReadOnlyList<InfoFormDefinition> ListFormDefinitions();

    IReadOnlyList<InfoClassImplementation> ListClassImplementations();
    InfoClassImplementation? GetClassImplementationByName(string name);
}
