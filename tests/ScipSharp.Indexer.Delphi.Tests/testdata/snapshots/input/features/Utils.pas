unit Utils;

interface

{$I Defs.inc}

uses
  SysUtils, Shapes;

type
  TLogger = class
  public
    procedure Log(const Msg: string); overload;
    procedure Log(const Fmt: string; const Args: array of const); overload;
  end;

  ILogSink = interface
    procedure Write(const S: string);
  end;

  TConsoleSink = class(TInterfacedObject, ILogSink)
  public
    procedure ILogSink.Write = WriteLine;
    procedure WriteLine(const S: string);
  end;

{$IFDEF USE_FANCY}
function Fancy(const S: string): string;
{$ELSE}
function Plain(const S: string): string;
{$ENDIF}

function TotalArea(List: TShapeList<TShape>): Double;
procedure Forwarded; forward;

implementation

procedure TLogger.Log(const Msg: string);
begin
  WriteLn(Msg);
end;

procedure TLogger.Log(const Fmt: string; const Args: array of const);
begin
  Log(Format(Fmt, Args));
end;

procedure TConsoleSink.WriteLine(const S: string);
begin
  System.WriteLn(S);
end;

{$IFDEF USE_FANCY}
function Fancy(const S: string): string;
begin
  Result := '** ' + S.ToUpper + ' **';
end;
{$ELSE}
function Plain(const S: string): string;
begin
  Result := S;
end;
{$ENDIF}

function TotalArea(List: TShapeList<TShape>): Double;
var
  Shape: TShape;
  Callback: TProc<TShape>;
begin
  Result := List.TotalArea;
  Callback := procedure(S: TShape)
    begin
      S.Draw(nil);
    end;
  for Shape in List do
    Callback(Shape);
  try
    Forwarded;
  except
    on E: EConvertError do
      WriteLn(E.Message);
  end;
end;

procedure Forwarded;
const
  Limit = MaxShapes;
var
  K: TShapeKind;
begin
  var Count := Limit div 2;
  K := skCircle;
  WriteLn(Count, K.ToString, Shapes.MaxShapes);
end;

end.
