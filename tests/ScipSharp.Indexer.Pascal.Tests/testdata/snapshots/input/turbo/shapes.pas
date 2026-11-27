{$IFDEF FPC}{$MODE TP}{$ENDIF}
unit Shapes;

interface

uses Crt;

const
  Bell = ^G;
  CrLf = #13#10;

type
  PShape = ^TShape;
  { Base object of the shape hierarchy }
  TShape = object
    X, Y: Integer;
    constructor Init(AX, AY: Integer);
    procedure Draw; virtual;
    procedure MoveTo(NX, NY: Integer);
    destructor Done; virtual;
  end;

  PCircle = ^TCircle;
  TCircle = object(TShape)
    Radius: Integer;
    constructor Init(AX, AY, R: Integer);
    procedure Draw; virtual;
  end;

  TName = string[20];

const
  Origin: TShape = (X: 0; Y: 0);

var
  ShapeCount: Word;

function Area(var C: TCircle): Real;

implementation

{$I colors.inc}

constructor TShape.Init(AX, AY: Integer);
begin
  X := AX;
  Y := AY;
  Inc(ShapeCount);
end;

procedure TShape.Draw;
begin
  TextColor(DefaultColor);
  GotoXY(X, Y);
  Write('*');
end;

procedure TShape.MoveTo(NX, NY: Integer);
begin
  Draw;
  X := NX;
  Y := NY;
  Draw;
end;

destructor TShape.Done;
begin
  Dec(ShapeCount);
end;

constructor TCircle.Init(AX, AY, R: Integer);
begin
  inherited Init(AX, AY);
  Radius := R;
end;

procedure TCircle.Draw;
begin
  inherited Draw;
  Write('(', Radius, ')');
end;

function Area(var C: TCircle): Real;
begin
  Area := Pi * Sqr(C.Radius);
end;

begin
  ShapeCount := 0;
end.
