unit Collections;

{$mode objfpc}{$H+}
{$modeswitch advancedrecords}

interface

uses
  Classes, SysUtils;

type
  { A 2D vector with operators }
  TVector = record
    X, Y: Double;
    function Length: Double;
    class function Make(AX, AY: Double): TVector; static;
  end;

  IShape = interface
    ['{6F1A3C7E-2B4D-4E8F-9A10-123456789ABC}']
    function Area: Double;
    function Describe: string;
  end;

  TShapeKind = (skCircle, skSquare);

  { Abstract base class }
  TShape = class(TInterfacedObject, IShape)
  private
    FName: string;
    FKind: TShapeKind;
  protected
    procedure SetName(const AValue: string); virtual;
  public
    constructor Create(const AName: string); virtual;
    function Area: Double; virtual; abstract;
    function Describe: string;
    property Name: string read FName write SetName;
    property Kind: TShapeKind read FKind;
  end;

  TCircle = class(TShape)
  private
    FRadius: Double; // radius in metres
  public
    constructor Create(const AName: string); override;
    function Area: Double; override;
    property Radius: Double read FRadius write FRadius;
  end;

  { Generic list with a default indexed property }
  generic TTypedList<T> = class
  private
    FItems: array of T;
    FCount: Integer;
    function GetItem(Index: Integer): T;
  public
    procedure Add(const AItem: T);
    function IndexOf(const AItem: T): Integer;
    property Items[Index: Integer]: T read GetItem; default;
    property Count: Integer read FCount;
  end;

  TShapeList = specialize TTypedList<TShape>;

  TShapeHelper = class helper for TShape
    function IsBig: Boolean;
  end;

operator + (const A, B: TVector) R: TVector;
function Max(A, B: Integer): Integer; overload;
function Max(A, B: Double): Double; overload;

implementation

function TVector.Length: Double;
begin
  Result := Sqrt(X * X + Y * Y);
end;

class function TVector.Make(AX, AY: Double): TVector;
begin
  Result.X := AX;
  Result.Y := AY;
end;

procedure TShape.SetName(const AValue: string);
begin
  FName := Trim(AValue);
end;

constructor TShape.Create(const AName: string);
begin
  inherited Create;
  Name := AName;
end;

function TShape.Describe: string;
begin
  Result := Format('%s (%.2f)', [FName, Area]);
end;

constructor TCircle.Create(const AName: string);
begin
  inherited Create(AName);
  FKind := skCircle;
  FRadius := 1.0;
end;

function TCircle.Area: Double;
begin
  Result := Pi * Sqr(FRadius);
end;

function TTypedList.GetItem(Index: Integer): T;
begin
  if (Index < 0) or (Index >= FCount) then
    raise EArgumentOutOfRangeException.CreateFmt('Index %d out of range', [Index]);
  Result := FItems[Index];
end;

procedure TTypedList.Add(const AItem: T);
begin
  if FCount = System.Length(FItems) then
    SetLength(FItems, FCount * 2 + 4);
  FItems[FCount] := AItem;
  Inc(FCount);
end;

function TTypedList.IndexOf(const AItem: T): Integer;
var
  I: Integer;
begin
  for I := 0 to FCount - 1 do
    if FItems[I] = AItem then
      Exit(I);
  Result := -1;
end;

function TShapeHelper.IsBig: Boolean;
begin
  Result := Area > 100;
end;

operator + (const A, B: TVector) R: TVector;
begin
  R.X := A.X + B.X;
  R.Y := A.Y + B.Y;
end;

function Max(A, B: Integer): Integer;
begin
  if A > B then Result := A else Result := B;
end;

function Max(A, B: Double): Double;
begin
  if A > B then Result := A else Result := B;
end;

end.
