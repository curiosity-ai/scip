//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Features/
  program Features;
//        ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Features/
//                 kind Module
//                 display_name Features
//                 signature_documentation
//                 > program Features
  
  {$APPTYPE CONSOLE}
  
  uses
    System.SysUtils,
//  ^^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/
    Shapes in 'Shapes.pas',
//  ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/
    Utils in 'Utils.pas',
//  ^^^^^ reference scip-delphi delphi fixture 1.0 Utils/
    MainForm in 'MainForm.pas' {FormMain};
//  ^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/
  
  var
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Features/Logger.
    Logger: TLogger;
//  ^^^^^^ definition scip-delphi delphi fixture 1.0 Features/Logger.
//         kind Variable
//         display_name Logger
//         signature_documentation
//         > var Logger: TLogger
//          ^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#
//                 ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Features/Logger.
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Features/Circle.
    Circle: TCircle;
//  ^^^^^^ definition scip-delphi delphi fixture 1.0 Features/Circle.
//         kind Variable
//         display_name Circle
//         signature_documentation
//         > var Circle: TCircle
//          ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#
//                 ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Features/Circle.
  
  begin
    Logger := TLogger.Create;
//  ^^^^^^ reference scip-delphi delphi fixture 1.0 Features/Logger.
//            ^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#
//                    ^^^^^^ reference scip-delphi delphi rtl . System/TObject#Create().
    try
      Circle := MakeCircle(1.5);
//    ^^^^^^ reference scip-delphi delphi fixture 1.0 Features/Circle.
//              ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/MakeCircle().
      Registry.Add(Circle);
//    ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/Registry.
//             ^^^ reference scip-delphi delphi rtl . System/Generics/Collections/TList#Add().
//                 ^^^^^^ reference scip-delphi delphi fixture 1.0 Features/Circle.
      Logger.Log('area %f', [TotalArea(Registry)]);
//    ^^^^^^ reference scip-delphi delphi fixture 1.0 Features/Logger.
//           ^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).
//                           ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/TotalArea().
//                                     ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/Registry.
      Logger.Log(Fancy(Circle.Name));
//    ^^^^^^ reference scip-delphi delphi fixture 1.0 Features/Logger.
//           ^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log().
//               ^^^^^ reference scip-delphi delphi fixture 1.0 Utils/Fancy().
//                     ^^^^^^ reference scip-delphi delphi fixture 1.0 Features/Circle.
//                            ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Name.
    finally
      Logger.Free;
//    ^^^^^^ reference scip-delphi delphi fixture 1.0 Features/Logger.
//           ^^^^ reference scip-delphi delphi rtl . System/TObject#Free().
    end;
  end.
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Features/
  
