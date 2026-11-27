  {$IFDEF FPC}{$MODE TP}{$ENDIF}
  unit Shapes;
//     ^^^^^^ definition scip-pascal . turbo . Shapes/
//            kind Module
//            display_name Shapes
//            signature_documentation
//            > unit Shapes
  
  interface
  
  uses Crt;
//     ^^^ reference scip-pascal fpc rtl . Crt/
  
  const
//  ⌄ enclosing_range_start scip-pascal . turbo . Shapes/Bell.
    Bell = ^G;
//  ^^^^ definition scip-pascal . turbo . Shapes/Bell.
//       kind Constant
//       display_name Bell
//       signature_documentation
//       > const Bell = ^G
//          ⌃ enclosing_range_end scip-pascal . turbo . Shapes/Bell.
//  ⌄ enclosing_range_start scip-pascal . turbo . Shapes/CrLf.
    CrLf = #13#10;
//  ^^^^ definition scip-pascal . turbo . Shapes/CrLf.
//       kind Constant
//       display_name CrLf
//       signature_documentation
//       > const CrLf = #13#10
//              ⌃ enclosing_range_end scip-pascal . turbo . Shapes/CrLf.
  
  type
//  ⌄ enclosing_range_start scip-pascal . turbo . Shapes/PShape#
    PShape = ^TShape;
//  ^^^^^^ definition scip-pascal . turbo . Shapes/PShape#
//         kind Type
//         display_name PShape
//         signature_documentation
//         > type PShape = ^TShape
//            ^^^^^^ reference scip-pascal . turbo . Shapes/TShape#
//                  ⌃ enclosing_range_end scip-pascal . turbo . Shapes/PShape#
    { Base object of the shape hierarchy }
//  ⌄ enclosing_range_start scip-pascal . turbo . Shapes/TShape#
    TShape = object
//  ^^^^^^ definition scip-pascal . turbo . Shapes/TShape#
//         kind Object
//         display_name TShape
//         signature_documentation
//         > type TShape = object
//         documentation
//         > Base object of the shape hierarchy
      X, Y: Integer;
//    ^ definition scip-pascal . turbo . Shapes/TShape#X.
//      kind Field
//      display_name X
//      signature_documentation
//      > X: Integer
//       ^ definition scip-pascal . turbo . Shapes/TShape#Y.
//         kind Field
//         display_name Y
//         signature_documentation
//         > Y: Integer
//          ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
      constructor Init(AX, AY: Integer);
//                ^^^^ forward_definition scip-pascal . turbo . Shapes/TShape#Init().
//                     ^^ definition local 0
//                        kind Parameter
//                        display_name AX
//                        signature_documentation
//                        > AX: Integer
//                         ^^ definition local 1
//                            kind Parameter
//                            display_name AY
//                            signature_documentation
//                            > AY: Integer
//                             ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
      procedure Draw; virtual;
//              ^^^^ forward_definition scip-pascal . turbo . Shapes/TShape#Draw().
      procedure MoveTo(NX, NY: Integer);
//              ^^^^^^ forward_definition scip-pascal . turbo . Shapes/TShape#MoveTo().
//                     ^^ definition local 2
//                        kind Parameter
//                        display_name NX
//                        signature_documentation
//                        > NX: Integer
//                         ^^ definition local 3
//                            kind Parameter
//                            display_name NY
//                            signature_documentation
//                            > NY: Integer
//                             ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
      destructor Done; virtual;
//               ^^^^ forward_definition scip-pascal . turbo . Shapes/TShape#Done().
    end;
//     ⌃ enclosing_range_end scip-pascal . turbo . Shapes/TShape#
  
//  ⌄ enclosing_range_start scip-pascal . turbo . Shapes/PCircle#
    PCircle = ^TCircle;
//  ^^^^^^^ definition scip-pascal . turbo . Shapes/PCircle#
//          kind Type
//          display_name PCircle
//          signature_documentation
//          > type PCircle = ^TCircle
//             ^^^^^^^ reference scip-pascal . turbo . Shapes/TCircle#
//                    ⌃ enclosing_range_end scip-pascal . turbo . Shapes/PCircle#
//  ⌄ enclosing_range_start scip-pascal . turbo . Shapes/TCircle#
    TCircle = object(TShape)
//  ^^^^^^^ definition scip-pascal . turbo . Shapes/TCircle#
//          kind Object
//          display_name TCircle
//          signature_documentation
//          > type TCircle = object(TShape)
//          relationship scip-pascal . turbo . Shapes/TShape# implementation
//                   ^^^^^^ reference scip-pascal . turbo . Shapes/TShape#
      Radius: Integer;
//    ^^^^^^ definition scip-pascal . turbo . Shapes/TCircle#Radius.
//           kind Field
//           display_name Radius
//           signature_documentation
//           > Radius: Integer
//            ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
      constructor Init(AX, AY, R: Integer);
//                ^^^^ forward_definition scip-pascal . turbo . Shapes/TCircle#Init().
//                     ^^ definition local 4
//                        kind Parameter
//                        display_name AX
//                        signature_documentation
//                        > AX: Integer
//                         ^^ definition local 5
//                            kind Parameter
//                            display_name AY
//                            signature_documentation
//                            > AY: Integer
//                             ^ definition local 6
//                               kind Parameter
//                               display_name R
//                               signature_documentation
//                               > R: Integer
//                                ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
      procedure Draw; virtual;
//              ^^^^ forward_definition scip-pascal . turbo . Shapes/TCircle#Draw().
    end;
//     ⌃ enclosing_range_end scip-pascal . turbo . Shapes/TCircle#
  
//  ⌄ enclosing_range_start scip-pascal . turbo . Shapes/TName#
    TName = string[20];
//  ^^^^^ definition scip-pascal . turbo . Shapes/TName#
//        kind TypeAlias
//        display_name TName
//        signature_documentation
//        > type TName = string[20]
//                    ⌃ enclosing_range_end scip-pascal . turbo . Shapes/TName#
  
  const
//  ⌄ enclosing_range_start scip-pascal . turbo . Shapes/Origin.
    Origin: TShape = (X: 0; Y: 0);
//  ^^^^^^ definition scip-pascal . turbo . Shapes/Origin.
//         kind Constant
//         display_name Origin
//         signature_documentation
//         > const Origin: TShape = (X: 0; Y: 0)
//          ^^^^^^ reference scip-pascal . turbo . Shapes/TShape#
//                    ^ reference scip-pascal . turbo . Shapes/TShape#X.
//                          ^ reference scip-pascal . turbo . Shapes/TShape#Y.
//                              ⌃ enclosing_range_end scip-pascal . turbo . Shapes/Origin.
  
  var
    ShapeCount: Word;
//  ^^^^^^^^^^ definition scip-pascal . turbo . Shapes/ShapeCount.
//             kind Variable
//             display_name ShapeCount
//             signature_documentation
//             > var ShapeCount: Word
//              ^^^^ reference scip-pascal fpc rtl . System/Word#
  
  function Area(var C: TCircle): Real;
//         ^^^^ forward_definition scip-pascal . turbo . Shapes/Area().
//                  ^ definition local 7
//                    kind Parameter
//                    display_name C
//                    signature_documentation
//                    > var C: TCircle
//                     ^^^^^^^ reference scip-pascal . turbo . Shapes/TCircle#
//                               ^^^^ reference scip-pascal fpc rtl . System/Real#
  
  implementation
  
  {$I colors.inc}
  
//⌄ enclosing_range_start scip-pascal . turbo . Shapes/TShape#Init().
  constructor TShape.Init(AX, AY: Integer);
//            ^^^^^^ reference scip-pascal . turbo . Shapes/TShape#
//                   ^^^^ definition scip-pascal . turbo . Shapes/TShape#Init().
//                        kind Constructor
//                        display_name Init
//                        signature_documentation
//                        > constructor Init(AX, AY: Integer)
//                        ^^ definition local 0
//                           kind Parameter
//                           display_name AX
//                           signature_documentation
//                           > AX: Integer
//                            ^^ definition local 1
//                               kind Parameter
//                               display_name AY
//                               signature_documentation
//                               > AY: Integer
//                                ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  begin
    X := AX;
//  ^ reference scip-pascal . turbo . Shapes/TShape#X.
//       ^^ reference local 0
    Y := AY;
//  ^ reference scip-pascal . turbo . Shapes/TShape#Y.
//       ^^ reference local 1
    Inc(ShapeCount);
//  ^^^ reference scip-pascal fpc rtl . System/Inc().
//      ^^^^^^^^^^ reference scip-pascal . turbo . Shapes/ShapeCount.
  end;
//  ⌃ enclosing_range_end scip-pascal . turbo . Shapes/TShape#Init().
  
//⌄ enclosing_range_start scip-pascal . turbo . Shapes/TShape#Draw().
  procedure TShape.Draw;
//          ^^^^^^ reference scip-pascal . turbo . Shapes/TShape#
//                 ^^^^ definition scip-pascal . turbo . Shapes/TShape#Draw().
//                      kind Method
//                      display_name Draw
//                      signature_documentation
//                      > procedure Draw
  begin
    TextColor(DefaultColor);
//  ^^^^^^^^^ reference scip-pascal fpc rtl . Crt/TextColor().
//            ^^^^^^^^^^^^ reference scip-pascal . turbo . Shapes/DefaultColor.
    GotoXY(X, Y);
//  ^^^^^^ reference scip-pascal fpc rtl . Crt/GotoXY().
//         ^ reference scip-pascal . turbo . Shapes/TShape#X.
//            ^ reference scip-pascal . turbo . Shapes/TShape#Y.
    Write('*');
//  ^^^^^ reference scip-pascal fpc rtl . System/Write().
  end;
//  ⌃ enclosing_range_end scip-pascal . turbo . Shapes/TShape#Draw().
  
//⌄ enclosing_range_start scip-pascal . turbo . Shapes/TShape#MoveTo().
  procedure TShape.MoveTo(NX, NY: Integer);
//          ^^^^^^ reference scip-pascal . turbo . Shapes/TShape#
//                 ^^^^^^ definition scip-pascal . turbo . Shapes/TShape#MoveTo().
//                        kind Method
//                        display_name MoveTo
//                        signature_documentation
//                        > procedure MoveTo(NX, NY: Integer)
//                        ^^ definition local 2
//                           kind Parameter
//                           display_name NX
//                           signature_documentation
//                           > NX: Integer
//                            ^^ definition local 3
//                               kind Parameter
//                               display_name NY
//                               signature_documentation
//                               > NY: Integer
//                                ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  begin
    Draw;
//  ^^^^ reference scip-pascal . turbo . Shapes/TShape#Draw().
    X := NX;
//  ^ reference scip-pascal . turbo . Shapes/TShape#X.
//       ^^ reference local 2
    Y := NY;
//  ^ reference scip-pascal . turbo . Shapes/TShape#Y.
//       ^^ reference local 3
    Draw;
//  ^^^^ reference scip-pascal . turbo . Shapes/TShape#Draw().
  end;
//  ⌃ enclosing_range_end scip-pascal . turbo . Shapes/TShape#MoveTo().
  
//⌄ enclosing_range_start scip-pascal . turbo . Shapes/TShape#Done().
  destructor TShape.Done;
//           ^^^^^^ reference scip-pascal . turbo . Shapes/TShape#
//                  ^^^^ definition scip-pascal . turbo . Shapes/TShape#Done().
//                       kind Method
//                       display_name Done
//                       signature_documentation
//                       > destructor Done
  begin
    Dec(ShapeCount);
//  ^^^ reference scip-pascal fpc rtl . System/Dec().
//      ^^^^^^^^^^ reference scip-pascal . turbo . Shapes/ShapeCount.
  end;
//  ⌃ enclosing_range_end scip-pascal . turbo . Shapes/TShape#Done().
  
//⌄ enclosing_range_start scip-pascal . turbo . Shapes/TCircle#Init().
  constructor TCircle.Init(AX, AY, R: Integer);
//            ^^^^^^^ reference scip-pascal . turbo . Shapes/TCircle#
//                    ^^^^ definition scip-pascal . turbo . Shapes/TCircle#Init().
//                         kind Constructor
//                         display_name Init
//                         signature_documentation
//                         > constructor Init(AX, AY, R: Integer)
//                         ^^ definition local 4
//                            kind Parameter
//                            display_name AX
//                            signature_documentation
//                            > AX: Integer
//                             ^^ definition local 5
//                                kind Parameter
//                                display_name AY
//                                signature_documentation
//                                > AY: Integer
//                                 ^ definition local 6
//                                   kind Parameter
//                                   display_name R
//                                   signature_documentation
//                                   > R: Integer
//                                    ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  begin
    inherited Init(AX, AY);
//            ^^^^ reference scip-pascal . turbo . Shapes/TShape#Init().
//                 ^^ reference local 4
//                     ^^ reference local 5
    Radius := R;
//  ^^^^^^ reference scip-pascal . turbo . Shapes/TCircle#Radius.
//            ^ reference local 6
  end;
//  ⌃ enclosing_range_end scip-pascal . turbo . Shapes/TCircle#Init().
  
//⌄ enclosing_range_start scip-pascal . turbo . Shapes/TCircle#Draw().
  procedure TCircle.Draw;
//          ^^^^^^^ reference scip-pascal . turbo . Shapes/TCircle#
//                  ^^^^ definition scip-pascal . turbo . Shapes/TCircle#Draw().
//                       kind Method
//                       display_name Draw
//                       signature_documentation
//                       > procedure Draw
//                       relationship scip-pascal . turbo . Shapes/TShape#Draw(). implementation
  begin
    inherited Draw;
//            ^^^^ reference scip-pascal . turbo . Shapes/TShape#Draw().
    Write('(', Radius, ')');
//  ^^^^^ reference scip-pascal fpc rtl . System/Write().
//             ^^^^^^ reference scip-pascal . turbo . Shapes/TCircle#Radius.
  end;
//  ⌃ enclosing_range_end scip-pascal . turbo . Shapes/TCircle#Draw().
  
//⌄ enclosing_range_start scip-pascal . turbo . Shapes/Area().
  function Area(var C: TCircle): Real;
//         ^^^^ definition scip-pascal . turbo . Shapes/Area().
//              kind Function
//              display_name Area
//              signature_documentation
//              > function Area(var C: TCircle): Real
//                  ^ definition local 7
//                    kind Parameter
//                    display_name C
//                    signature_documentation
//                    > var C: TCircle
//                     ^^^^^^^ reference scip-pascal . turbo . Shapes/TCircle#
//                               ^^^^ reference scip-pascal fpc rtl . System/Real#
  begin
    Area := Pi * Sqr(C.Radius);
//  ^^^^ reference scip-pascal . turbo . Shapes/Area().
//          ^^ reference scip-pascal fpc rtl . System/Pi().
//               ^^^ reference scip-pascal fpc rtl . System/Sqr().
//                   ^ reference local 7
//                     ^^^^^^ reference scip-pascal . turbo . Shapes/TCircle#Radius.
  end;
//  ⌃ enclosing_range_end scip-pascal . turbo . Shapes/Area().
  
  begin
    ShapeCount := 0;
//  ^^^^^^^^^^ reference scip-pascal . turbo . Shapes/ShapeCount.
  end.
  
