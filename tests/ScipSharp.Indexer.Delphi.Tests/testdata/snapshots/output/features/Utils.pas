//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/
  unit Utils;
//     ^^^^^ definition scip-delphi delphi fixture 1.0 Utils/
//           kind Module
//           display_name Utils
//           signature_documentation
//           > unit Utils
  
  interface
  
  {$I Defs.inc}
  
  uses
    SysUtils, Shapes;
//  ^^^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/
//            ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/
  
  type
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/TLogger#
    TLogger = class
//  ^^^^^^^ definition scip-delphi delphi fixture 1.0 Utils/TLogger#
//          kind Class
//          display_name TLogger
//          signature_documentation
//          > type TLogger = class
    public
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/TLogger#Log().
      procedure Log(const Msg: string); overload;
//              ^^^ definition scip-delphi delphi fixture 1.0 Utils/TLogger#Log().
//                  kind Method
//                  display_name Log
//                  signature_documentation
//                  > procedure Log(const Msg: string); overload
//                        ^^^ definition scip-delphi delphi fixture 1.0 Utils/TLogger#Log().(Msg)
//                            kind Parameter
//                            display_name Msg
//                            signature_documentation
//                            > const Msg: string
//                             ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                              ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/TLogger#Log().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).
      procedure Log(const Fmt: string; const Args: array of const); overload;
//              ^^^ definition scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).
//                  kind Method
//                  display_name Log
//                  signature_documentation
//                  > procedure Log(const Fmt: string; const Args: array of const); overload
//                        ^^^ definition scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).(Fmt)
//                            kind Parameter
//                            display_name Fmt
//                            signature_documentation
//                            > const Fmt: string
//                             ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                           ^^^^ definition scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).(Args)
//                                                kind Parameter
//                                                display_name Args
//                                                signature_documentation
//                                                > const Args: array of const
//                                                                          ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/TLogger#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/ILogSink#
    ILogSink = interface
//  ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Utils/ILogSink#
//           kind Interface
//           display_name ILogSink
//           signature_documentation
//           > type ILogSink = interface
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/ILogSink#Write().
      procedure Write(const S: string);
//              ^^^^^ definition scip-delphi delphi fixture 1.0 Utils/ILogSink#Write().
//                    kind Method
//                    display_name Write
//                    signature_documentation
//                    > procedure Write(const S: string)
//                          ^ definition scip-delphi delphi fixture 1.0 Utils/ILogSink#Write().(S)
//                            kind Parameter
//                            display_name S
//                            signature_documentation
//                            > const S: string
//                             ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                    ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/ILogSink#Write().
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/ILogSink#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/TConsoleSink#
    TConsoleSink = class(TInterfacedObject, ILogSink)
//  ^^^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Utils/TConsoleSink#
//               kind Class
//               display_name TConsoleSink
//               signature_documentation
//               > type TConsoleSink = class(TInterfacedObject, ILogSink)
//               relationship scip-delphi delphi fixture 1.0 Utils/ILogSink# implementation
//               relationship scip-delphi delphi rtl . System/TInterfacedObject# implementation
//                       ^^^^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/TInterfacedObject#
//                                          ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/ILogSink#
    public
      procedure ILogSink.Write = WriteLine;
//              ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/ILogSink#
//                       ^^^^^ reference scip-delphi delphi fixture 1.0 Utils/ILogSink#Write().
//                               ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/TConsoleSink#WriteLine().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/TConsoleSink#WriteLine().
      procedure WriteLine(const S: string);
//              ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Utils/TConsoleSink#WriteLine().
//                        kind Method
//                        display_name WriteLine
//                        signature_documentation
//                        > procedure WriteLine(const S: string)
//                        relationship scip-delphi delphi fixture 1.0 Utils/ILogSink#Write(). implementation
//                              ^ definition scip-delphi delphi fixture 1.0 Utils/TConsoleSink#WriteLine().(S)
//                                kind Parameter
//                                display_name S
//                                signature_documentation
//                                > const S: string
//                                 ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                        ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/TConsoleSink#WriteLine().
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/TConsoleSink#
  
  {$IFDEF USE_FANCY}
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/Fancy().
  function Fancy(const S: string): string;
//         ^^^^^ definition scip-delphi delphi fixture 1.0 Utils/Fancy().
//               kind Function
//               display_name Fancy
//               signature_documentation
//               > function Fancy(const S: string): string
//                     ^ definition scip-delphi delphi fixture 1.0 Utils/Fancy().(S)
//                       kind Parameter
//                       display_name S
//                       signature_documentation
//                       > const S: string
//                        ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                 ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                       ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/Fancy().
  {$ELSE}
  function Plain(const S: string): string;
  {$ENDIF}
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/TotalArea().
  function TotalArea(List: TShapeList<TShape>): Double;
//         ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Utils/TotalArea().
//                   kind Function
//                   display_name TotalArea
//                   signature_documentation
//                   > function TotalArea(List: TShapeList<TShape>): Double
//                   ^^^^ definition scip-delphi delphi fixture 1.0 Utils/TotalArea().(List)
//                        kind Parameter
//                        display_name List
//                        signature_documentation
//                        > List: TShapeList<TShape>
//                         ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#
//                                    ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                                              ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                                                    ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/TotalArea().
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/Forwarded().
  procedure Forwarded; forward;
//          ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Utils/Forwarded().
//                    kind Function
//                    display_name Forwarded
//                    signature_documentation
//                    > procedure Forwarded
//                            ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/Forwarded().
  
  implementation
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/TLogger#Log().
  procedure TLogger.Log(const Msg: string);
//          ^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#
//                  ^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log().
//                            ^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log().(Msg)
//                                 ^^^^^^ reference scip-delphi delphi rtl . System/string#
  begin
    WriteLn(Msg);
//  ^^^^^^^ reference scip-delphi delphi rtl . System/WriteLn().
//          ^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log().(Msg)
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/TLogger#Log().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).
  procedure TLogger.Log(const Fmt: string; const Args: array of const);
//          ^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#
//                  ^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).
//                            ^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).(Fmt)
//                                 ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                               ^^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).(Args)
  begin
    Log(Format(Fmt, Args));
//  ^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log().
//      ^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/Format().
//             ^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).(Fmt)
//                  ^^^^ reference scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).(Args)
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/TLogger#Log(+1).
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/TConsoleSink#WriteLine().
  procedure TConsoleSink.WriteLine(const S: string);
//          ^^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/TConsoleSink#
//                       ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/TConsoleSink#WriteLine().
//                                       ^ reference scip-delphi delphi fixture 1.0 Utils/TConsoleSink#WriteLine().(S)
//                                          ^^^^^^ reference scip-delphi delphi rtl . System/string#
  begin
    System.WriteLn(S);
//  ^^^^^^ reference scip-delphi delphi rtl . System/
//         ^^^^^^^ reference scip-delphi delphi rtl . System/WriteLn().
//                 ^ reference scip-delphi delphi fixture 1.0 Utils/TConsoleSink#WriteLine().(S)
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/TConsoleSink#WriteLine().
  
  {$IFDEF USE_FANCY}
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/Fancy().
  function Fancy(const S: string): string;
//         ^^^^^ reference scip-delphi delphi fixture 1.0 Utils/Fancy().
//                     ^ reference scip-delphi delphi fixture 1.0 Utils/Fancy().(S)
//                        ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                 ^^^^^^ reference scip-delphi delphi rtl . System/string#
  begin
    Result := '** ' + S.ToUpper + ' **';
//                    ^ reference scip-delphi delphi fixture 1.0 Utils/Fancy().(S)
//                      ^^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/TStringHelper#ToUpper().
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/Fancy().
  {$ELSE}
  function Plain(const S: string): string;
  begin
    Result := S;
  end;
  {$ENDIF}
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/TotalArea().
  function TotalArea(List: TShapeList<TShape>): Double;
//         ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/TotalArea().
//                   ^^^^ reference scip-delphi delphi fixture 1.0 Utils/TotalArea().(List)
//                         ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#
//                                    ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                                              ^^^^^^ reference scip-delphi delphi rtl . System/Double#
  var
//  ⌄ enclosing_range_start local 0
    Shape: TShape;
//  ^^^^^ definition local 0
//        kind Variable
//        display_name Shape
//        signature_documentation
//        > Shape: TShape
//         ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//               ⌃ enclosing_range_end local 0
//  ⌄ enclosing_range_start local 1
    Callback: TProc<TShape>;
//  ^^^^^^^^ definition local 1
//           kind Variable
//           display_name Callback
//           signature_documentation
//           > Callback: TProc<TShape>
//            ^^^^^ reference scip-delphi delphi rtl . System/TProc-1#
//                  ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                         ⌃ enclosing_range_end local 1
  begin
    Result := List.TotalArea;
//            ^^^^ reference scip-delphi delphi fixture 1.0 Utils/TotalArea().(List)
//                 ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#TotalArea().
    Callback := procedure(S: TShape)
//  ^^^^^^^^ reference local 1
//                        ^ definition local 2
//                          kind Parameter
//                          display_name S
//                          signature_documentation
//                          > S: TShape
//                           ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
      begin
        S.Draw(nil);
//      ^ reference local 2
//        ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().
      end;
    for Shape in List do
//      ^^^^^ reference local 0
//               ^^^^ reference scip-delphi delphi fixture 1.0 Utils/TotalArea().(List)
      Callback(Shape);
//    ^^^^^^^^ reference local 1
//             ^^^^^ reference local 0
    try
      Forwarded;
//    ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/Forwarded().
    except
      on E: EConvertError do
//       ^ definition local 3
//         kind Variable
//         display_name E
//         signature_documentation
//         > E: EConvertError
//          ^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/EConvertError#
        WriteLn(E.Message);
//      ^^^^^^^ reference scip-delphi delphi rtl . System/WriteLn().
//              ^ reference local 3
//                ^^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/Exception#Message.
    end;
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/TotalArea().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utils/Forwarded().
  procedure Forwarded;
//          ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Utils/Forwarded().
  const
//  ⌄ enclosing_range_start local 4
    Limit = MaxShapes;
//  ^^^^^ definition local 4
//        kind Constant
//        display_name Limit
//        signature_documentation
//        > const Limit = MaxShapes
//          ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/MaxShapes.
//                   ⌃ enclosing_range_end local 4
  var
//  ⌄ enclosing_range_start local 5
    K: TShapeKind;
//  ^ definition local 5
//    kind Variable
//    display_name K
//    signature_documentation
//    > K: TShapeKind
//     ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeKind#
//               ⌃ enclosing_range_end local 5
  begin
    var Count := Limit div 2;
//      ^^^^^ definition local 6
//            kind Variable
//            display_name Count
//            signature_documentation
//            > Count
//               ^^^^^ reference local 4
    K := skCircle;
//  ^ reference local 5
//       ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeKind#skCircle.
    WriteLn(Count, K.ToString, Shapes.MaxShapes);
//  ^^^^^^^ reference scip-delphi delphi rtl . System/WriteLn().
//          ^^^^^ reference local 6
//                 ^ reference local 5
//                   ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TKindHelper#ToString().
//                             ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/
//                                    ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/MaxShapes.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/Forwarded().
  
  end.
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utils/
  
