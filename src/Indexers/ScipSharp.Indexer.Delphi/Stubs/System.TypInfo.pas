// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.TypInfo;

interface

type
  TTypeKind = (tkUnknown, tkInteger, tkChar, tkEnumeration, tkFloat, tkString, tkSet, tkClass, tkMethod, tkWChar,
    tkLString, tkWString, tkVariant, tkArray, tkRecord, tkInterface, tkInt64, tkDynArray, tkUString, tkClassRef,
    tkPointer, tkProcedure, tkMRecord);
  TTypeKinds = set of TTypeKind;

  TTypeData = record
    MinValue: Integer;
    MaxValue: Integer;
  end;
  PTypeData = ^TTypeData;

  TTypeInfo = record
    Kind: TTypeKind;
    Name: ShortString;
    function TypeData: PTypeData;
  end;
  PTypeInfo = ^TTypeInfo;
  PPTypeInfo = ^PTypeInfo;

  TPropInfo = record
    PropType: PPTypeInfo;
    Name: ShortString;
  end;
  PPropInfo = ^TPropInfo;

function GetTypeData(TypeInfo: PTypeInfo): PTypeData;
function GetEnumName(TypeInfo: PTypeInfo; Value: Integer): string;
function GetEnumValue(TypeInfo: PTypeInfo; const Name: string): Integer;
function GetPropInfo(Instance: TObject; const PropName: string): PPropInfo; overload;
function GetPropValue(Instance: TObject; const PropName: string): Variant; overload;
procedure SetPropValue(Instance: TObject; const PropName: string; const Value: Variant); overload;
function GetOrdProp(Instance: TObject; const PropName: string): NativeInt; overload;
function GetStrProp(Instance: TObject; const PropName: string): string; overload;
function IsPublishedProp(Instance: TObject; const PropName: string): Boolean; overload;

implementation

end.
