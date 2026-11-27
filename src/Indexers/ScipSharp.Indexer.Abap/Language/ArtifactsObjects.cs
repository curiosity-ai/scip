// Ported from abaplint packages/core/src/artifacts_objects.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
// Only the object types the syntax check needs are ported; the other types upstream knows become a
// GenericObject (registered, not parsed), unknown types an UnknownObject as upstream.
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap;

public static class ArtifactsObjects
{
    private static readonly Dictionary<string, Func<string, IObject>> objectMap = new()
    {
        ["AUTH"] = n => new AuthorizationCheckField(n),
        ["BDEF"] = n => new BehaviorDefinition(n),
        ["CLAS"] = n => new Class(n),
        ["DDLS"] = n => new DataDefinition(n),
        ["DOMA"] = n => new Domain(n),
        ["DTEL"] = n => new DataElement(n),
        ["ENHS"] = n => new EnhancementSpot(n),
        ["ENQU"] = n => new LockObject(n),
        ["FUGR"] = n => new FunctionGroup(n),
        ["INTF"] = n => new Interface(n),
        ["MSAG"] = n => new MessageClass(n),
        ["PROG"] = n => new Program(n),
        ["TABL"] = n => new Table(n),
        ["TTYP"] = n => new Objects.TableType(n),
        ["TYPE"] = n => new TypePool(n),
        ["VIEW"] = n => new View(n),
    };

    // object types of packages/core/src/objects that are not ported
    private static readonly HashSet<string> notPorted = new((
        "ACID APIS APLO AQBG AQQU AQSG AREA ASFC AVAR AVAS BOBF CDBO CHAR CHDO CHKC CHKO CHKV CMOD CUS0 CUS1 CUS2 " +
        "DCLS DDLX DEVC DIAL DOCT DOCV DRTY DSYS DTEB ECAT ECTC ECTD EEEC ENHC ENHO ENSC EVTB FDT0 FORM G4BA HTTP IAMU " +
        "IARP IASP IATU IDOC IEXT IOBJ IWMO IWOM IWPR IWSG IWSV IWVB JOBD NROB NSPC OA2P ODSO PARA PDTS PERS PINF SAJC " +
        "SAJT SAMC SAPC SCO1 SCO2 SCO3 SCP1 SCVI SFBF SFBS SFPF SFPI SFSW SHI3 SHI5 SHI8 SHLP SHMA SIA1 SIA2 SIA3 SIA5 " +
        "SIA6 SIA7 SICF SKTD SMBC SMIM SMTG SOBJ SOTS SPLO SPPF SPRX SRFC SRVB SRVD SSFO SSST STVI STYL SUCU SUSC SUSH " +
        "SUSO SXCI SXSD TOBJ TRAN UENO UIAD VCLS W3HT W3MI WAPA WDCA WDCC WDYA WDYN WEBI XINX XSLT ZN00 ZN01 ZN02 ZN03 " +
        "ZN04 ZN05 ZN06 ZN07 ZN08 ZN09 ZN10 ZN11 ZN12 ZN13 ZN14 ZN15 ZN16 ZN17 ZN18 ZN19 ZN20 ZN21 ZN22").Split(' '));

    public static IObject NewObject(string name, string? type)
    {
        if (type != null && objectMap.TryGetValue(type, out var create))
        {
            return create(name);
        }
        if (type != null && notPorted.Contains(type))
        {
            return new GenericObject(name, type);
        }
        return new UnknownObject(name, type!);
    }
}
