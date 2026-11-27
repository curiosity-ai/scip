unit Syntax;

interface

uses
{$IFDEF MSWINDOWS}
  Winapi.Windows, Winapi.Messages,
{$ENDIF}
  System.SysUtils, System.Generics.Collections;

type
  TVector = record
    X, Y: Double;
    constructor Create(AX, AY: Double);
    class operator Add(const A, B: TVector): TVector;
    class operator Implicit(const V: TVector): string;
    function Length: Double;
  end;

  TOuter = class
  public
    type
      TInner<T> = class
        Value: T;
        function Echo(const A: T): T;
      end;
  strict private
    [weak] FParent: TOuter;
    class var FInstances: Integer;
    function GetCoord(Index: Integer): Integer;
    procedure SetCoord(Index: Integer; Value: Integer);
  public
    procedure WMPaint(var Msg: TWMPaint); message WM_PAINT;
    class property Instances: Integer read FInstances;
    property Left: Integer index 0 read GetCoord write SetCoord;
    property Top: Integer index 1 read GetCoord write SetCoord;
    property Parent: TOuter read FParent;
  end;

  TMatrix = TArray<TArray<Integer>>;
  PVector = ^TVector;
  TVectorProc = reference to procedure(const V: TVector);

function Beep(Frequency, Duration: Cardinal): LongBool; stdcall; external 'kernel32.dll' name 'Beep';
procedure Demo(Obj: TObject);

implementation

constructor TVector.Create(AX, AY: Double);
begin
  X := AX;
  Y := AY;
end;

class operator TVector.Add(const A, B: TVector): TVector;
begin
  Result := TVector.Create(A.X + B.X, A.Y + B.Y);
end;

class operator TVector.Implicit(const V: TVector): string;
begin
  Result := Format('(%f, %f)', [V.X, V.Y]);
end;

function TVector.Length: Double;
begin
  Result := Sqrt(Sqr(X) + Sqr(Y));
end;

function TOuter.TInner<T>.Echo(const A: T): T;
begin
  Value := A;
  Result := Value;
end;

function TOuter.GetCoord(Index: Integer): Integer;
begin
  Result := Index;
end;

procedure TOuter.SetCoord(Index: Integer; Value: Integer);
begin
end;

procedure TOuter.WMPaint(var Msg: TWMPaint);
begin
  Msg.Result := 0;
end;

procedure Demo(Obj: TObject);
label
  Done;
var
  V: TVector;
  P: PVector;
  M: TMatrix;
  Inner: TOuter.TInner<string>;
  Callback: TVectorProc;
begin
  V := TVector.Create(1, 2) + TVector.Create(3, 4);
  P := @V;
  WriteLn(P^.Length, P.X);
  SetLength(M, 2, 2);
  M[0][1] := 1;
  Inner := TOuter.TInner<string>.Create;
  WriteLn(Inner.Echo('hi').Length);
  if Obj is TOuter then
    WriteLn((Obj as TOuter).Left, TOuter(Obj).Top, TOuter.Instances);
  Callback := procedure(const W: TVector) begin WriteLn(W.Length); end;
  Callback(V);
  case Round(V.X) of
    0..3: goto Done;
  else
    raise EArgumentException.Create('too big');
  end;
  try
    asm
      nop
    end;
  except
    on E: Exception do
      raise;
  end;
Done:
end;

end.
