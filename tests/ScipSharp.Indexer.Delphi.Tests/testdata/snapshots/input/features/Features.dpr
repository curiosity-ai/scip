program Features;

{$APPTYPE CONSOLE}

uses
  System.SysUtils,
  Shapes in 'Shapes.pas',
  Utils in 'Utils.pas',
  MainForm in 'MainForm.pas' {FormMain};

var
  Logger: TLogger;
  Circle: TCircle;

begin
  Logger := TLogger.Create;
  try
    Circle := MakeCircle(1.5);
    Registry.Add(Circle);
    Logger.Log('area %f', [TotalArea(Registry)]);
    Logger.Log(Fancy(Circle.Name));
  finally
    Logger.Free;
  end;
end.
