// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.Variants;

interface

function VarIsNull(const V: Variant): Boolean;
function VarIsEmpty(const V: Variant): Boolean;
function VarIsClear(const V: Variant): Boolean;
function VarIsArray(const A: Variant): Boolean;
function VarIsStr(const V: Variant): Boolean;
function VarIsNumeric(const V: Variant): Boolean;
function VarType(const V: Variant): Word;
function VarToStr(const V: Variant): string;
function VarToStrDef(const V: Variant; const ADefault: string): string;
function VarArrayCreate(const Bounds: array of Integer; AVarType: Word): Variant;
function VarArrayOf(const Values: array of Variant): Variant;
function VarArrayLowBound(const A: Variant; Dim: Integer): Integer;
function VarArrayHighBound(const A: Variant; Dim: Integer): Integer;
procedure VarClear(var V: Variant);
function Null: Variant;
function Unassigned: Variant;

implementation

end.
