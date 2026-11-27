program Draw;

uses Crt, Shapes;

var
  C: PCircle;
  S: TShape;
  Title: TName;
  Screen: array[1..25, 1..80] of Word absolute $B800:0000;

procedure Pause;
var
  Ch: Char;
begin
  Ch := ReadKey;
  if Ch = #0 then
    Ch := ReadKey;
end;

begin
  ClrScr;
  New(C, Init(10, 10, 5));
  C^.Draw;
  S.Init(1, 1);
  with S do
  begin
    MoveTo(X + 1, Y + 1);
    Done;
  end;
  Title := 'Circle';
  WriteLn(Title, ': ', Area(C^):0:2, Bell);
  Dispose(C, Done);
  Pause;
  asm
    mov ax, 3
    int $10
  end;
end.
