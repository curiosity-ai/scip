// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit System.StrUtils;

interface

uses System.Types;

function LeftStr(const AText: string; const ACount: Integer): string;
function RightStr(const AText: string; const ACount: Integer): string;
function MidStr(const AText: string; const AStart, ACount: Integer): string;
function StartsStr(const ASubText, AText: string): Boolean;
function EndsStr(const ASubText, AText: string): Boolean;
function StartsText(const ASubText, AText: string): Boolean;
function EndsText(const ASubText, AText: string): Boolean;
function ContainsStr(const AText, ASubText: string): Boolean;
function ContainsText(const AText, ASubText: string): Boolean;
function ReplaceStr(const AText, AFromText, AToText: string): string;
function ReplaceText(const AText, AFromText, AToText: string): string;
function ReverseString(const AText: string): string;
function DupeString(const AText: string; ACount: Integer): string;
function IfThen(AValue: Boolean; const ATrue: string; const AFalse: string = ''): string;
function IndexStr(const AText: string; const AValues: array of string): Integer;
function IndexText(const AText: string; const AValues: array of string): Integer;
function MatchStr(const AText: string; const AValues: array of string): Boolean;
function MatchText(const AText: string; const AValues: array of string): Boolean;
function PosEx(const SubStr, S: string; Offset: Integer = 1): Integer;
function SplitString(const S, Delimiters: string): TStringDynArray;
function AnsiReplaceStr(const AText, AFromText, AToText: string): string;
function AnsiContainsText(const AText, ASubText: string): Boolean;
function AnsiStartsText(const ASubText, AText: string): Boolean;

implementation

end.
