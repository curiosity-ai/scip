unit Shapes;

{$SCOPEDENUMS ON}

interface

uses
  System.SysUtils, System.Classes, System.Generics.Collections;

type
  /// <summary>Colours a shape can have.</summary>
  TShapeColor = (Red, Green, Blue);

{$SCOPEDENUMS OFF}

  TShapeKind = (skCircle, skSquare);
  TShapeKinds = set of TShapeKind;

  TShape = class;

  /// <summary>Something that can be drawn.</summary>
  IDrawable = interface
    ['{6A1F5B2C-3D4E-4F60-8A9B-0C1D2E3F4A5B}']
    procedure Draw(const Canvas: TStrings);
    function GetName: string;
    property Name: string read GetName;
  end;

  TShapeEvent = procedure(Sender: TShape) of object;

  [Serializable]
  TShape = class abstract(TInterfacedObject, IDrawable)
  private
    FName: string;
    FColor: TShapeColor;
    FOnChange: TShapeEvent;
    procedure SetName(const Value: string);
  protected
    function GetName: string; virtual;
  public
    type
      TPoint = record
        X, Y: Integer;
      end;
    const
      DefaultName = 'shape';
    class var Count: Integer;
    constructor Create(const AName: string); virtual;
    destructor Destroy; override;
    /// <summary>Area of the shape.</summary>
    /// <returns>The area in square units.</returns>
    function Area: Double; virtual; abstract;
    procedure Draw(const Canvas: TStrings); virtual;
    procedure Move(DX, DY: Integer); overload;
    procedure Move(const P: TPoint); overload;
    class function Describe(Shape: TShape): string; static;
    property Name: string read GetName write SetName;
    property Color: TShapeColor read FColor write FColor default TShapeColor.Red;
    property OnChange: TShapeEvent read FOnChange write FOnChange;
  end;

  TCircle = class(TShape)
  private
    FRadius: Double;
  public
    constructor Create(const AName: string; ARadius: Double); reintroduce;
    function Area: Double; override;
    property Radius: Double read FRadius;
  end;

  TShapeList<T: TShape> = class(TObjectList<T>)
  public
    function TotalArea: Double;
    function Largest: T;
  end;

  TShapeHelper = class helper for TShape
    function IsBig: Boolean;
  end;

  TKindHelper = record helper for TShapeKind
    function ToString: string;
  end;

resourcestring
  SUnnamed = 'unnamed';

const
  MaxShapes = 100;

var
  Registry: TShapeList<TShape>;

threadvar
  LastError: string;

function MakeCircle(Radius: Double): TCircle;

implementation

uses
  System.Math;

{ TShape }

constructor TShape.Create(const AName: string);
begin
  inherited Create;
  FName := AName;
  Inc(Count);
end;

destructor TShape.Destroy;
begin
  Dec(Count);
  inherited;
end;

function TShape.GetName: string;
begin
  if FName = '' then
    Result := SUnnamed
  else
    Result := FName;
end;

procedure TShape.SetName(const Value: string);
begin
  FName := Value;
  if Assigned(FOnChange) then
    FOnChange(Self);
end;

procedure TShape.Draw(const Canvas: TStrings);
begin
  Canvas.Add(Format('%s: %.2f', [Name, Area]));
end;

procedure TShape.Move(DX, DY: Integer);
begin
end;

procedure TShape.Move(const P: TPoint);
begin
  Move(P.X, P.Y);
end;

class function TShape.Describe(Shape: TShape): string;
begin
  Result := Shape.Name + ' ' + Shape.ClassName;
end;

{ TCircle }

constructor TCircle.Create(const AName: string; ARadius: Double);
begin
  inherited Create(AName);
  FRadius := ARadius;
end;

function TCircle.Area: Double;
begin
  Result := Pi * Sqr(FRadius);
end;

{ TShapeList<T> }

function TShapeList<T>.TotalArea: Double;
begin
  Result := 0;
  for var S in Self do
    Result := Result + S.Area;
end;

function TShapeList<T>.Largest: T;
var
  I: Integer;
begin
  Result := nil;
  for I := 0 to Count - 1 do
    if (Result = nil) or (Items[I].Area > Result.Area) then
      Result := Items[I];
end;

{ TShapeHelper }

function TShapeHelper.IsBig: Boolean;
begin
  Result := Area > 10;
end;

function TKindHelper.ToString: string;
begin
  case Self of
    skCircle: Result := 'circle';
    skSquare: Result := 'square';
  end;
end;

function MakeCircle(Radius: Double): TCircle;
begin
  Result := TCircle.Create(SUnnamed, Max(Radius, 0));
  with Result do
    Color := TShapeColor.Blue;
end;

initialization
  Registry := TShapeList<TShape>.Create;

finalization
  FreeAndNil(Registry);

end.
