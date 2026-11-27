program Demo;

{$mode objfpc}{$H+}

uses
  {$IFDEF UNIX}cthreads,{$ENDIF}
  Interfaces, Forms, SysUtils, Collections, MainForm;

var
  List: TShapeList;
  Shape: TShape;
  V: TVector;
  I: Integer;
begin
  List := TShapeList.Create;
  try
    List.Add(TCircle.Create('unit circle'));
    for I := 0 to List.Count - 1 do
    begin
      Shape := List[I];
      WriteLn(Shape.Describe, ' big=', Shape.IsBig);
      if Shape is TCircle then
        WriteLn(TCircle(Shape).Radius:0:2);
    end;
    V := TVector.Make(1, 2) + TVector.Make(3, 4);
    WriteLn(V.Length:0:3, ' ', Max(1, 2), ' ', Max(1.5, 2.5):0:1);
  except
    on E: Exception do
      WriteLn('error: ', E.Message);
  end;
  List.Free;
  RequireDerivedFormResource := True;
  Application.Initialize;
  Application.CreateForm(TForm1, Form1);
  Application.Run;
end.
