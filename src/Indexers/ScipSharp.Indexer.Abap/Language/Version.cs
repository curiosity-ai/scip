// Ported from abaplint packages/core/src/version.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap;

/// <summary>Deprecated release identifiers, use <see cref="ABAPRelease"/> and <see cref="Release"/> instead.</summary>
public enum Version
{
    /// <summary>"open-abap", as 702 but with some extra new language features.</summary>
    OpenABAP,
    v700,
    v702,
    v740sp02,
    v740sp05,
    v740sp08,
    v750,
    v751,
    v752,
    v753,
    v754,
    v755,
    v756,
    v757,
    v758,
    v816,
    /// <summary>Steampunk / SAP BTP ABAP Environment, expands to Release.Newest + LanguageVersion.Cloud.</summary>
    Cloud,
}

/// <summary>Dialect axis. Ordering: KeyUser(0) &lt; Cloud(1) &lt; Normal(2), lower = more restrictive.</summary>
public enum LanguageVersion
{
    Normal,
    KeyUser,
    Cloud,
}

/// <summary>A single row in the release table, see version.ts for the column meanings.</summary>
public sealed record ABAPRelease(string Name, string? Abap, int? Kernel, int? Op, int? Cloud, int? Ce, int Ordinal)
{
    public override string ToString() => Name;
}

public static class VersionExtensions
{
    /// <summary>The TS string value of the enum, e.g. "open-abap", "v740sp05".</summary>
    public static string ToStr(this Version v) => v == Version.OpenABAP ? "open-abap" : v.ToString();

    public static Version ParseVersion(string s)
    {
        if (s == "open-abap") return Version.OpenABAP;
        return Enum.Parse<Version>(s);
    }
}

/// <summary>Lookup table of releases, <c>Release.v740sp05</c>, <c>Release.Newest</c>, ...</summary>
public static class Release
{
    private static readonly (string Name, string? Abap, int? Kernel, int? Op, int? Cloud, int? Ce)[] Raw =
    [
        ("v700", "700", 700, 700, null, null),
        ("v701", "701", 701, 701, null, null),
        ("v702", "702", 702, 702, null, null),
        ("open-abap", "open-abap", null, null, null, null),
        ("v710", "710", 710, 710, null, null),
        ("v711", "711", 711, 711, null, null),
        ("v720", "720", 720, 720, null, null),
        ("v722", "722", 722, 722, null, null),
        ("v730", "730", 730, 730, null, null),
        ("v731", "731", 731, 731, null, null),
        ("v740sp02", "740SP02", 740, 740, null, null),
        ("v740sp05", "740SP05", 741, 740, null, null),
        ("v740sp08", "740SP08", 742, 740, null, null),
        ("v760", "7.60", 743, 750, 760, null),
        ("v761", "7.61", 744, 750, 761, null),
        ("v762", "7.62", 745, 750, 762, null),
        ("v763", "7.63", 746, 751, 763, null),
        ("v764", "7.64", 747, 751, 764, null),
        ("v765", "7.65", 748, 751, 765, null),
        ("v766", "7.66", 749, 752, 766, null),
        ("v767", "7.67", 750, 752, 767, 1702),
        ("v768", "7.68", 751, 752, 768, 1705),
        ("v769", "7.69", 752, 752, 769, 1708),
        ("v770", "7.70", 753, 753, 770, 1711),
        ("v771", "7.71", 771, 753, 771, 1802),
        ("v772", "7.72", 772, 753, 772, 1805),
        ("v773", "7.73", 773, 753, 773, 1808),
        ("v774", "7.74", 774, 754, 774, 1811),
        ("v775", "7.75", 775, 754, 775, 1902),
        ("v776", "7.76", 776, 754, 776, 1905),
        ("v777", "7.77", 777, 754, 777, 1908),
        ("v778", "7.78", 778, 755, 778, 1911),
        ("v779", "7.79", 779, 755, 779, 2002),
        ("v780", "7.80", 780, 755, 780, 2005),
        ("v781", "7.81", 781, 755, 781, 2008),
        ("v782", "7.82", 782, 756, 782, 2011),
        ("v783", "7.83", 783, 756, 783, 2102),
        ("v784", "7.84", 784, 756, 784, 2105),
        ("v785", "7.85", 785, 756, 785, 2108),
        ("v786", "7.86", 786, 757, 786, 2111),
        ("v787", "7.87", 787, 757, 787, 2202),
        ("v788", "7.88", 788, 757, 788, 2205),
        ("v789", "7.89", 789, 757, 789, 2208),
        ("v790", "7.90", 790, 758, 790, 2211),
        ("v791", "7.91", 791, 758, 791, 2302),
        ("v792", "7.92", 792, 758, 792, 2305),
        ("v793", "7.93", 793, 758, 793, 2308),
        ("v794", "7.94", 794, 816, 794, 2311),
        ("v795", "7.95", 795, 816, 795, 2402),
        ("v796", "7.96", 796, 816, 796, 2405),
        ("v912", "9.12", 912, 816, 912, 2408),
        ("v913", "9.13", 913, 816, 913, 2411),
        ("v914", "9.14", 914, 816, 914, 2502),
        ("v915", "9.15", 915, 816, 915, 2505),
        ("v916", "9.16", 916, 816, 916, 2508),
        ("v917", "9.17", 917, null, 917, 2511),
        ("v918", "9.18", 918, null, 918, 2602),
        ("Newest", null, null, null, null, null),
    ];

    /// <summary>All known releases in chronological order.</summary>
    public static readonly IReadOnlyList<ABAPRelease> ReleaseList =
        Raw.Select((r, i) => new ABAPRelease(r.Name, r.Abap, r.Kernel, r.Op, r.Cloud, r.Ce, i)).ToArray();

    private static readonly Dictionary<string, ABAPRelease> ByName = BuildByName();

    private static Dictionary<string, ABAPRelease> BuildByName()
    {
        var ret = ReleaseList.ToDictionary(r => r.Name);
        // deprecated aliases: on-prem v750-v758 map to the last cloud row at that op
        ret["v750"] = ret["v762"];
        ret["v751"] = ret["v765"];
        ret["v752"] = ret["v769"];
        ret["v753"] = ret["v773"];
        ret["v754"] = ret["v777"];
        ret["v755"] = ret["v781"];
        ret["v756"] = ret["v785"];
        ret["v757"] = ret["v789"];
        ret["v758"] = ret["v793"];
        ret["v816"] = ret["v916"];
        return ret;
    }

    /// <summary>TS <c>Release[name]</c>, e.g. <c>Release.Get("open-abap")</c>.</summary>
    public static ABAPRelease Get(string name) => ByName[name];

    public static bool TryGet(string name, out ABAPRelease release) => ByName.TryGetValue(name, out release!);

    public static IEnumerable<string> Names => ByName.Keys;

    public static ABAPRelease v700 => ByName["v700"];
    public static ABAPRelease v701 => ByName["v701"];
    public static ABAPRelease v702 => ByName["v702"];
    /// <summary>TS <c>Release["open-abap"]</c>.</summary>
    public static ABAPRelease OpenABAP => ByName["open-abap"];
    public static ABAPRelease v710 => ByName["v710"];
    public static ABAPRelease v711 => ByName["v711"];
    public static ABAPRelease v720 => ByName["v720"];
    public static ABAPRelease v722 => ByName["v722"];
    public static ABAPRelease v730 => ByName["v730"];
    public static ABAPRelease v731 => ByName["v731"];
    public static ABAPRelease v740sp02 => ByName["v740sp02"];
    public static ABAPRelease v740sp05 => ByName["v740sp05"];
    public static ABAPRelease v740sp08 => ByName["v740sp08"];
    public static ABAPRelease v750 => ByName["v750"];
    public static ABAPRelease v751 => ByName["v751"];
    public static ABAPRelease v752 => ByName["v752"];
    public static ABAPRelease v753 => ByName["v753"];
    public static ABAPRelease v754 => ByName["v754"];
    public static ABAPRelease v755 => ByName["v755"];
    public static ABAPRelease v756 => ByName["v756"];
    public static ABAPRelease v757 => ByName["v757"];
    public static ABAPRelease v758 => ByName["v758"];
    public static ABAPRelease v760 => ByName["v760"];
    public static ABAPRelease v761 => ByName["v761"];
    public static ABAPRelease v762 => ByName["v762"];
    public static ABAPRelease v763 => ByName["v763"];
    public static ABAPRelease v764 => ByName["v764"];
    public static ABAPRelease v765 => ByName["v765"];
    public static ABAPRelease v766 => ByName["v766"];
    public static ABAPRelease v767 => ByName["v767"];
    public static ABAPRelease v768 => ByName["v768"];
    public static ABAPRelease v769 => ByName["v769"];
    public static ABAPRelease v770 => ByName["v770"];
    public static ABAPRelease v771 => ByName["v771"];
    public static ABAPRelease v772 => ByName["v772"];
    public static ABAPRelease v773 => ByName["v773"];
    public static ABAPRelease v774 => ByName["v774"];
    public static ABAPRelease v775 => ByName["v775"];
    public static ABAPRelease v776 => ByName["v776"];
    public static ABAPRelease v777 => ByName["v777"];
    public static ABAPRelease v778 => ByName["v778"];
    public static ABAPRelease v779 => ByName["v779"];
    public static ABAPRelease v780 => ByName["v780"];
    public static ABAPRelease v781 => ByName["v781"];
    public static ABAPRelease v782 => ByName["v782"];
    public static ABAPRelease v783 => ByName["v783"];
    public static ABAPRelease v784 => ByName["v784"];
    public static ABAPRelease v785 => ByName["v785"];
    public static ABAPRelease v786 => ByName["v786"];
    public static ABAPRelease v787 => ByName["v787"];
    public static ABAPRelease v788 => ByName["v788"];
    public static ABAPRelease v789 => ByName["v789"];
    public static ABAPRelease v790 => ByName["v790"];
    public static ABAPRelease v791 => ByName["v791"];
    public static ABAPRelease v792 => ByName["v792"];
    public static ABAPRelease v793 => ByName["v793"];
    public static ABAPRelease v794 => ByName["v794"];
    public static ABAPRelease v795 => ByName["v795"];
    public static ABAPRelease v796 => ByName["v796"];
    public static ABAPRelease v816 => ByName["v816"];
    public static ABAPRelease v912 => ByName["v912"];
    public static ABAPRelease v913 => ByName["v913"];
    public static ABAPRelease v914 => ByName["v914"];
    public static ABAPRelease v915 => ByName["v915"];
    public static ABAPRelease v916 => ByName["v916"];
    public static ABAPRelease v917 => ByName["v917"];
    public static ABAPRelease v918 => ByName["v918"];
    /// <summary>Always-active sentinel, satisfies any release check.</summary>
    public static ABAPRelease Newest => ByName["Newest"];
}

public static class Versions
{
    /// <summary>Is <paramref name="running"/> at least as new as <paramref name="required"/>? Compared by ordinal.</summary>
    public static bool ReleaseAtLeast(ABAPRelease running, ABAPRelease required) => running.Ordinal >= required.Ordinal;

    public static ABAPRelease VersionToABAPRelease(Version v) => v switch
    {
        Version.v700 => Release.v700,
        Version.v702 => Release.v702,
        Version.v740sp02 => Release.v740sp02,
        Version.v740sp05 => Release.v740sp05,
        Version.v740sp08 => Release.v740sp08,
        Version.v750 => Release.v750,
        Version.v751 => Release.v751,
        Version.v752 => Release.v752,
        Version.v753 => Release.v753,
        Version.v754 => Release.v754,
        Version.v755 => Release.v755,
        Version.v756 => Release.v756,
        Version.v757 => Release.v757,
        Version.v758 => Release.v758,
        Version.v816 => Release.v816,
        Version.Cloud => Release.Newest,
        Version.OpenABAP => Release.OpenABAP,
        _ => throw new ArgumentException("No ABAPRelease mapping for Version: " + v),
    };

    public static readonly ABAPRelease DefaultRelease = Release.Newest;

    public static Version GetPreviousVersion(Version v)
    {
        if (v == Version.OpenABAP)
        {
            return Version.v702;
        }
        var all = Enum.GetValues<Version>();
        var found = Array.IndexOf(all, v);
        if (found < 0) throw new ArgumentException("Unknown version: " + v);
        if (found == 0) throw new ArgumentException("Nothing lower: " + v);
        return all[found - 1];
    }
}
