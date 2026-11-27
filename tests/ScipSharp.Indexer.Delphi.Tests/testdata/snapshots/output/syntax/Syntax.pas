//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/
  unit Syntax;
//     ^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/
//            kind Module
//            display_name Syntax
//            signature_documentation
//            > unit Syntax
  
  interface
  
  uses
  {$IFDEF MSWINDOWS}
    Winapi.Windows, Winapi.Messages,
//  ^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . Winapi/Windows/
//                  ^^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . Winapi/Messages/
  {$ENDIF}
    System.SysUtils, System.Generics.Collections;
//  ^^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/
//                   ^^^^^^^^^^^^^^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/Generics/Collections/
  
  type
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#
    TVector = record
//  ^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#
//          kind Struct
//          display_name TVector
//          signature_documentation
//          > type TVector = record
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#X.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#Y.
      X, Y: Double;
//    ^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#X.
//      kind Field
//      display_name X
//      signature_documentation
//      > X: Double
//       ^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#Y.
//         kind Field
//         display_name Y
//         signature_documentation
//         > Y: Double
//          ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#X.
//                ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#Y.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#Create().
      constructor Create(AX, AY: Double);
//                ^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#Create().
//                       kind Constructor
//                       display_name Create
//                       signature_documentation
//                       > constructor Create(AX, AY: Double)
//                       ^^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#Create().(AX)
//                          kind Parameter
//                          display_name AX
//                          signature_documentation
//                          > AX: Double
//                           ^^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#Create().(AY)
//                              kind Parameter
//                              display_name AY
//                              signature_documentation
//                              > AY: Double
//                               ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                                      ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#Create().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#Add().
      class operator Add(const A, B: TVector): TVector;
//                   ^^^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#Add().
//                       kind Operator
//                       display_name Add
//                       signature_documentation
//                       > class operator Add(const A, B: TVector): TVector
//                             ^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#Add().(A)
//                               kind Parameter
//                               display_name A
//                               signature_documentation
//                               > const A: TVector
//                                ^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#Add().(B)
//                                  kind Parameter
//                                  display_name B
//                                  signature_documentation
//                                  > const B: TVector
//                                   ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                                             ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                                                    ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#Add().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#Implicit().
      class operator Implicit(const V: TVector): string;
//                   ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#Implicit().
//                            kind Operator
//                            display_name Implicit
//                            signature_documentation
//                            > class operator Implicit(const V: TVector): string
//                                  ^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#Implicit().(V)
//                                    kind Parameter
//                                    display_name V
//                                    signature_documentation
//                                    > const V: TVector
//                                     ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                                               ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                                     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#Implicit().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#Length().
      function Length: Double;
//             ^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TVector#Length().
//                    kind Method
//                    display_name Length
//                    signature_documentation
//                    > function Length: Double
//                     ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                           ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#Length().
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#
    TOuter = class
//  ^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#
//         kind Class
//         display_name TOuter
//         signature_documentation
//         > type TOuter = class
    public
      type
//      ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#
        TInner<T> = class
//      ^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#
//             kind Class
//             display_name TInner
//             signature_documentation
//             > type TInner<T> = class
//             ^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#[T]
//               kind TypeParameter
//               display_name T
//               signature_documentation
//               > T
//        ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Value.
          Value: T;
//        ^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Value.
//              kind Field
//              display_name Value
//              signature_documentation
//              > Value: T
//               ^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#[T]
//                ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Value.
//        ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Echo().
          function Echo(const A: T): T;
//                 ^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Echo().
//                      kind Method
//                      display_name Echo
//                      signature_documentation
//                      > function Echo(const A: T): T
//                            ^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Echo().(A)
//                              kind Parameter
//                              display_name A
//                              signature_documentation
//                              > const A: T
//                               ^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#[T]
//                                   ^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#[T]
//                                    ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Echo().
        end;
//         ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#
    strict private
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#FParent.
      [weak] FParent: TOuter;
//           ^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#FParent.
//                   kind Field
//                   display_name FParent
//                   signature_documentation
//                   > FParent: TOuter
//                    ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                          ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#FParent.
//              ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#FInstances.
      class var FInstances: Integer;
//              ^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#FInstances.
//                         kind StaticField
//                         display_name FInstances
//                         signature_documentation
//                         > class var FInstances: Integer
//                          ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                 ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#FInstances.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().
      function GetCoord(Index: Integer): Integer;
//             ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().
//                      kind Method
//                      display_name GetCoord
//                      signature_documentation
//                      > function GetCoord(Index: Integer): Integer
//                      ^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().(Index)
//                            kind Parameter
//                            display_name Index
//                            signature_documentation
//                            > Index: Integer
//                             ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                       ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                              ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().
      procedure SetCoord(Index: Integer; Value: Integer);
//              ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().
//                       kind Method
//                       display_name SetCoord
//                       signature_documentation
//                       > procedure SetCoord(Index: Integer; Value: Integer)
//                       ^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().(Index)
//                             kind Parameter
//                             display_name Index
//                             signature_documentation
//                             > Index: Integer
//                              ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                       ^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().(Value)
//                                             kind Parameter
//                                             display_name Value
//                                             signature_documentation
//                                             > Value: Integer
//                                              ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                                      ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().
    public
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#WMPaint().
      procedure WMPaint(var Msg: TWMPaint); message WM_PAINT;
//              ^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#WMPaint().
//                      kind Method
//                      display_name WMPaint
//                      signature_documentation
//                      > procedure WMPaint(var Msg: TWMPaint)
//                          ^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#WMPaint().(Msg)
//                              kind Parameter
//                              display_name Msg
//                              signature_documentation
//                              > var Msg: TWMPaint
//                               ^^^^^^^^ reference scip-delphi delphi rtl . Winapi/Messages/TWMPaint#
//                                                  ^^^^^^^^ reference scip-delphi delphi rtl . Winapi/Messages/WM_PAINT.
//                                                          ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#WMPaint().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#Instances.
      class property Instances: Integer read FInstances;
//                   ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#Instances.
//                             kind StaticProperty
//                             display_name Instances
//                             signature_documentation
//                             > class property Instances: Integer read FInstances
//                              ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                           ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#FInstances.
//                                                     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#Instances.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#Left.
      property Left: Integer index 0 read GetCoord write SetCoord;
//             ^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#Left.
//                  kind Property
//                  display_name Left
//                  signature_documentation
//                  > property Left: Integer index 0 read GetCoord write SetCoord
//                   ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                        ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().
//                                                       ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().
//                                                               ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#Left.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#Top.
      property Top: Integer index 1 read GetCoord write SetCoord;
//             ^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#Top.
//                 kind Property
//                 display_name Top
//                 signature_documentation
//                 > property Top: Integer index 1 read GetCoord write SetCoord
//                  ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                       ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().
//                                                      ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().
//                                                              ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#Top.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#Parent.
      property Parent: TOuter read FParent;
//             ^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TOuter#Parent.
//                    kind Property
//                    display_name Parent
//                    signature_documentation
//                    > property Parent: TOuter read FParent
//                     ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                                 ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#FParent.
//                                        ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#Parent.
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TMatrix#
    TMatrix = TArray<TArray<Integer>>;
//  ^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TMatrix#
//          kind TypeAlias
//          display_name TMatrix
//          signature_documentation
//          > type TMatrix = TArray<TArray<Integer>>
//            ^^^^^^ reference scip-delphi delphi rtl . System/Generics/Collections/TArray#
//                   ^^^^^^ reference scip-delphi delphi rtl . System/Generics/Collections/TArray#
//                          ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TMatrix#
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/PVector#
    PVector = ^TVector;
//  ^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/PVector#
//          kind TypeAlias
//          display_name PVector
//          signature_documentation
//          > type PVector = ^TVector
//             ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                    ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/PVector#
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVectorProc#
    TVectorProc = reference to procedure(const V: TVector);
//  ^^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/TVectorProc#
//              kind Delegate
//              display_name TVectorProc
//              signature_documentation
//              > type TVectorProc = reference to procedure(const V: TVector)
//                                             ^ definition scip-delphi delphi fixture 1.0 Syntax/TVectorProc#(V)
//                                               kind Parameter
//                                               display_name V
//                                               signature_documentation
//                                               > const V: TVector
//                                                ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                                                        ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVectorProc#
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/Beep().
  function Beep(Frequency, Duration: Cardinal): LongBool; stdcall; external 'kernel32.dll' name 'Beep';
//         ^^^^ definition scip-delphi delphi fixture 1.0 Syntax/Beep().
//              kind Function
//              display_name Beep
//              signature_documentation
//              > function Beep(Frequency, Duration: Cardinal): LongBool
//              ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/Beep().(Frequency)
//                        kind Parameter
//                        display_name Frequency
//                        signature_documentation
//                        > Frequency: Cardinal
//                         ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Syntax/Beep().(Duration)
//                                  kind Parameter
//                                  display_name Duration
//                                  signature_documentation
//                                  > Duration: Cardinal
//                                   ^^^^^^^^ reference scip-delphi delphi rtl . System/Cardinal#
//                                              ^^^^^^^^ reference scip-delphi delphi rtl . System/LongBool#
//                                                                                                    ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/Beep().
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/Demo().
  procedure Demo(Obj: TObject);
//          ^^^^ definition scip-delphi delphi fixture 1.0 Syntax/Demo().
//               kind Function
//               display_name Demo
//               signature_documentation
//               > procedure Demo(Obj: TObject)
//               ^^^ definition scip-delphi delphi fixture 1.0 Syntax/Demo().(Obj)
//                   kind Parameter
//                   display_name Obj
//                   signature_documentation
//                   > Obj: TObject
//                    ^^^^^^^ reference scip-delphi delphi rtl . System/TObject#
//                            ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/Demo().
  
  implementation
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#Create().
  constructor TVector.Create(AX, AY: Double);
//            ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                    ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Create().
//                           ^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Create().(AX)
//                               ^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Create().(AY)
//                                   ^^^^^^ reference scip-delphi delphi rtl . System/Double#
  begin
    X := AX;
//  ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#X.
//       ^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Create().(AX)
    Y := AY;
//  ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Y.
//       ^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Create().(AY)
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#Create().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#Add().
  class operator TVector.Add(const A, B: TVector): TVector;
//               ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                       ^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Add().
//                                 ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Add().(A)
//                                    ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Add().(B)
//                                       ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                                                 ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
  begin
    Result := TVector.Create(A.X + B.X, A.Y + B.Y);
//            ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                    ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Create().
//                           ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Add().(A)
//                             ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#X.
//                                 ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Add().(B)
//                                   ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#X.
//                                      ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Add().(A)
//                                        ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Y.
//                                            ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Add().(B)
//                                              ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Y.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#Add().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#Implicit().
  class operator TVector.Implicit(const V: TVector): string;
//               ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                       ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Implicit().
//                                      ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Implicit().(V)
//                                         ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                                                   ^^^^^^ reference scip-delphi delphi rtl . System/string#
  begin
    Result := Format('(%f, %f)', [V.X, V.Y]);
//            ^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/Format().
//                                ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Implicit().(V)
//                                  ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#X.
//                                     ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Implicit().(V)
//                                       ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Y.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#Implicit().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TVector#Length().
  function TVector.Length: Double;
//         ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                 ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Length().
//                         ^^^^^^ reference scip-delphi delphi rtl . System/Double#
  begin
    Result := Sqrt(Sqr(X) + Sqr(Y));
//            ^^^^ reference scip-delphi delphi rtl . System/Sqrt().
//                 ^^^ reference scip-delphi delphi rtl . System/Sqr().
//                     ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#X.
//                          ^^^ reference scip-delphi delphi rtl . System/Sqr().
//                              ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Y.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TVector#Length().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Echo().
  function TOuter.TInner<T>.Echo(const A: T): T;
//         ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#
//                       ^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#[T]
//                          ^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Echo().
//                                     ^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Echo().(A)
//                                        ^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#[T]
//                                            ^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#[T]
  begin
    Value := A;
//  ^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Value.
//           ^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Echo().(A)
    Result := Value;
//            ^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Value.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Echo().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().
  function TOuter.GetCoord(Index: Integer): Integer;
//         ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().
//                         ^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().(Index)
//                                ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                          ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
  begin
    Result := Index;
//            ^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().(Index)
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#GetCoord().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().
  procedure TOuter.SetCoord(Index: Integer; Value: Integer);
//          ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                 ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().
//                          ^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().(Index)
//                                 ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                          ^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().(Value)
//                                                 ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
  begin
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#SetCoord().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/TOuter#WMPaint().
  procedure TOuter.WMPaint(var Msg: TWMPaint);
//          ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                 ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#WMPaint().
//                             ^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#WMPaint().(Msg)
//                                  ^^^^^^^^ reference scip-delphi delphi rtl . Winapi/Messages/TWMPaint#
  begin
    Msg.Result := 0;
//  ^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#WMPaint().(Msg)
//      ^^^^^^ reference scip-delphi delphi rtl . Winapi/Messages/TWMPaint#Result.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/TOuter#WMPaint().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Syntax/Demo().
  procedure Demo(Obj: TObject);
//          ^^^^ reference scip-delphi delphi fixture 1.0 Syntax/Demo().
//               ^^^ reference scip-delphi delphi fixture 1.0 Syntax/Demo().(Obj)
//                    ^^^^^^^ reference scip-delphi delphi rtl . System/TObject#
  label
    Done;
//  ^^^^ definition local 0
//       display_name Done
  var
//  ⌄ enclosing_range_start local 1
    V: TVector;
//  ^ definition local 1
//    kind Variable
//    display_name V
//    signature_documentation
//    > V: TVector
//     ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//            ⌃ enclosing_range_end local 1
//  ⌄ enclosing_range_start local 2
    P: PVector;
//  ^ definition local 2
//    kind Variable
//    display_name P
//    signature_documentation
//    > P: PVector
//     ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/PVector#
//            ⌃ enclosing_range_end local 2
//  ⌄ enclosing_range_start local 3
    M: TMatrix;
//  ^ definition local 3
//    kind Variable
//    display_name M
//    signature_documentation
//    > M: TMatrix
//     ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TMatrix#
//            ⌃ enclosing_range_end local 3
//  ⌄ enclosing_range_start local 4
    Inner: TOuter.TInner<string>;
//  ^^^^^ definition local 4
//        kind Variable
//        display_name Inner
//        signature_documentation
//        > Inner: TOuter.TInner<string>
//         ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#
//                       ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                              ⌃ enclosing_range_end local 4
//  ⌄ enclosing_range_start local 5
    Callback: TVectorProc;
//  ^^^^^^^^ definition local 5
//           kind Variable
//           display_name Callback
//           signature_documentation
//           > Callback: TVectorProc
//            ^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVectorProc#
//                       ⌃ enclosing_range_end local 5
  begin
    V := TVector.Create(1, 2) + TVector.Create(3, 4);
//  ^ reference local 1
//       ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//               ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Create().
//                              ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                                      ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Create().
    P := @V;
//  ^ reference local 2
//        ^ reference local 1
    WriteLn(P^.Length, P.X);
//  ^^^^^^^ reference scip-delphi delphi rtl . System/WriteLn().
//          ^ reference local 2
//             ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Length().
//                     ^ reference local 2
//                       ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#X.
    SetLength(M, 2, 2);
//  ^^^^^^^^^ reference scip-delphi delphi rtl . System/SetLength().
//            ^ reference local 3
    M[0][1] := 1;
//  ^ reference local 3
    Inner := TOuter.TInner<string>.Create;
//  ^^^^^ reference local 4
//           ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                  ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#
//                         ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                 ^^^^^^ reference scip-delphi delphi rtl . System/TObject#Create().
    WriteLn(Inner.Echo('hi').Length);
//  ^^^^^^^ reference scip-delphi delphi rtl . System/WriteLn().
//          ^^^^^ reference local 4
//                ^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#TInner#Echo().
//                           ^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/TStringHelper#Length.
    if Obj is TOuter then
//     ^^^ reference scip-delphi delphi fixture 1.0 Syntax/Demo().(Obj)
//            ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
      WriteLn((Obj as TOuter).Left, TOuter(Obj).Top, TOuter.Instances);
//    ^^^^^^^ reference scip-delphi delphi rtl . System/WriteLn().
//             ^^^ reference scip-delphi delphi fixture 1.0 Syntax/Demo().(Obj)
//                    ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                            ^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#Left.
//                                  ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                                         ^^^ reference scip-delphi delphi fixture 1.0 Syntax/Demo().(Obj)
//                                              ^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#Top.
//                                                   ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#
//                                                          ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TOuter#Instances.
    Callback := procedure(const W: TVector) begin WriteLn(W.Length); end;
//  ^^^^^^^^ reference local 5
//                              ^ definition local 6
//                                kind Parameter
//                                display_name W
//                                signature_documentation
//                                > const W: TVector
//                                 ^^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#
//                                                ^^^^^^^ reference scip-delphi delphi rtl . System/WriteLn().
//                                                        ^ reference local 6
//                                                          ^^^^^^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#Length().
    Callback(V);
//  ^^^^^^^^ reference local 5
//           ^ reference local 1
    case Round(V.X) of
//       ^^^^^ reference scip-delphi delphi rtl . System/Round().
//             ^ reference local 1
//               ^ reference scip-delphi delphi fixture 1.0 Syntax/TVector#X.
      0..3: goto Done;
//               ^^^^ reference local 0
    else
      raise EArgumentException.Create('too big');
//          ^^^^^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/EArgumentException#
//                             ^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/Exception#Create().
    end;
    try
      asm
        nop
      end;
    except
      on E: Exception do
//       ^ definition local 7
//         kind Variable
//         display_name E
//         signature_documentation
//         > E: Exception
//          ^^^^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/Exception#
        raise;
    end;
  Done:
//^^^^ reference local 0
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/Demo().
  
  end.
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Syntax/
  
