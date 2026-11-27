  unit Collections;
//     ^^^^^^^^^^^ definition scip-pascal lazarus Demo . Collections/
//                 kind Module
//                 display_name Collections
//                 signature_documentation
//                 > unit Collections
  
  {$mode objfpc}{$H+}
  {$modeswitch advancedrecords}
  
  interface
  
  uses
    Classes, SysUtils;
//  ^^^^^^^ reference scip-pascal fpc rtl . Classes/
//           ^^^^^^^^ reference scip-pascal fpc rtl . SysUtils/
  
  type
    { A 2D vector with operators }
//  ⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TVector#
    TVector = record
//  ^^^^^^^ definition scip-pascal lazarus Demo . Collections/TVector#
//          kind Struct
//          display_name TVector
//          signature_documentation
//          > type TVector = record
//          documentation
//          > A 2D vector with operators
      X, Y: Double;
//    ^ definition scip-pascal lazarus Demo . Collections/TVector#X.
//      kind Field
//      display_name X
//      signature_documentation
//      > X: Double
//       ^ definition scip-pascal lazarus Demo . Collections/TVector#Y.
//         kind Field
//         display_name Y
//         signature_documentation
//         > Y: Double
//          ^^^^^^ reference scip-pascal fpc rtl . System/Double#
      function Length: Double;
//             ^^^^^^ forward_definition scip-pascal lazarus Demo . Collections/TVector#Length().
//                     ^^^^^^ reference scip-pascal fpc rtl . System/Double#
      class function Make(AX, AY: Double): TVector; static;
//                   ^^^^ forward_definition scip-pascal lazarus Demo . Collections/TVector#Make().
//                        ^^ definition local 0
//                           kind Parameter
//                           display_name AX
//                           signature_documentation
//                           > AX: Double
//                            ^^ definition local 1
//                               kind Parameter
//                               display_name AY
//                               signature_documentation
//                               > AY: Double
//                                ^^^^^^ reference scip-pascal fpc rtl . System/Double#
//                                         ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
    end;
//     ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TVector#
  
//  ⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/IShape#
    IShape = interface
//  ^^^^^^ definition scip-pascal lazarus Demo . Collections/IShape#
//         kind Interface
//         display_name IShape
//         signature_documentation
//         > type IShape = interface
      ['{6F1A3C7E-2B4D-4E8F-9A10-123456789ABC}']
      function Area: Double;
//             ^^^^ definition scip-pascal lazarus Demo . Collections/IShape#Area().
//                  kind Method
//                  display_name Area
//                  signature_documentation
//                  > function Area: Double
//                   ^^^^^^ reference scip-pascal fpc rtl . System/Double#
      function Describe: string;
//             ^^^^^^^^ definition scip-pascal lazarus Demo . Collections/IShape#Describe().
//                      kind Method
//                      display_name Describe
//                      signature_documentation
//                      > function Describe: string
    end;
//     ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/IShape#
  
//  ⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TShapeKind#
    TShapeKind = (skCircle, skSquare);
//  ^^^^^^^^^^ definition scip-pascal lazarus Demo . Collections/TShapeKind#
//             kind Enum
//             display_name TShapeKind
//             signature_documentation
//             > type TShapeKind = (skCircle, skSquare)
//                ^^^^^^^^ definition scip-pascal lazarus Demo . Collections/TShapeKind#skCircle.
//                         kind EnumMember
//                         display_name skCircle
//                         signature_documentation
//                         > TShapeKind.skCircle
//                          ^^^^^^^^ definition scip-pascal lazarus Demo . Collections/TShapeKind#skSquare.
//                                   kind EnumMember
//                                   display_name skSquare
//                                   signature_documentation
//                                   > TShapeKind.skSquare
//                                   ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TShapeKind#
  
    { Abstract base class }
//  ⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TShape#
    TShape = class(TInterfacedObject, IShape)
//  ^^^^^^ definition scip-pascal lazarus Demo . Collections/TShape#
//         kind Class
//         display_name TShape
//         signature_documentation
//         > type TShape = class(TInterfacedObject, IShape)
//         documentation
//         > Abstract base class
//         relationship scip-pascal fpc rtl . System/TInterfacedObject# implementation
//         relationship scip-pascal lazarus Demo . Collections/IShape# implementation
//                 ^^^^^^^^^^^^^^^^^ reference scip-pascal fpc rtl . System/TInterfacedObject#
//                                    ^^^^^^ reference scip-pascal lazarus Demo . Collections/IShape#
    private
      FName: string;
//    ^^^^^ definition scip-pascal lazarus Demo . Collections/TShape#FName.
//          kind Field
//          display_name FName
//          signature_documentation
//          > FName: string
      FKind: TShapeKind;
//    ^^^^^ definition scip-pascal lazarus Demo . Collections/TShape#FKind.
//          kind Field
//          display_name FKind
//          signature_documentation
//          > FKind: TShapeKind
//           ^^^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TShapeKind#
    protected
      procedure SetName(const AValue: string); virtual;
//              ^^^^^^^ forward_definition scip-pascal lazarus Demo . Collections/TShape#SetName().
//                            ^^^^^^ definition local 2
//                                   kind Parameter
//                                   display_name AValue
//                                   signature_documentation
//                                   > const AValue: string
    public
      constructor Create(const AName: string); virtual;
//                ^^^^^^ forward_definition scip-pascal lazarus Demo . Collections/TShape#Create().
//                             ^^^^^ definition local 3
//                                   kind Parameter
//                                   display_name AName
//                                   signature_documentation
//                                   > const AName: string
      function Area: Double; virtual; abstract;
//             ^^^^ definition scip-pascal lazarus Demo . Collections/TShape#Area().
//                  kind AbstractMethod
//                  display_name Area
//                  signature_documentation
//                  > function Area: Double
//                  relationship scip-pascal lazarus Demo . Collections/IShape#Area(). implementation
//                   ^^^^^^ reference scip-pascal fpc rtl . System/Double#
      function Describe: string;
//             ^^^^^^^^ forward_definition scip-pascal lazarus Demo . Collections/TShape#Describe().
      property Name: string read FName write SetName;
//             ^^^^ definition scip-pascal lazarus Demo . Collections/TShape#Name.
//                  kind Property
//                  display_name Name
//                  signature_documentation
//                  > property Name: string
//                               ^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#FName.
//                                           ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#SetName().
      property Kind: TShapeKind read FKind;
//             ^^^^ definition scip-pascal lazarus Demo . Collections/TShape#Kind.
//                  kind Property
//                  display_name Kind
//                  signature_documentation
//                  > property Kind: TShapeKind
//                   ^^^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TShapeKind#
//                                   ^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#FKind.
    end;
//     ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TShape#
  
//  ⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TCircle#
    TCircle = class(TShape)
//  ^^^^^^^ definition scip-pascal lazarus Demo . Collections/TCircle#
//          kind Class
//          display_name TCircle
//          signature_documentation
//          > type TCircle = class(TShape)
//          relationship scip-pascal lazarus Demo . Collections/TShape# implementation
//                  ^^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#
    private
      FRadius: Double; // radius in metres
//    ^^^^^^^ definition scip-pascal lazarus Demo . Collections/TCircle#FRadius.
//            kind Field
//            display_name FRadius
//            signature_documentation
//            > FRadius: Double
//            documentation
//            > radius in metres
//             ^^^^^^ reference scip-pascal fpc rtl . System/Double#
    public
      constructor Create(const AName: string); override;
//                ^^^^^^ forward_definition scip-pascal lazarus Demo . Collections/TCircle#Create().
//                             ^^^^^ definition local 4
//                                   kind Parameter
//                                   display_name AName
//                                   signature_documentation
//                                   > const AName: string
      function Area: Double; override;
//             ^^^^ forward_definition scip-pascal lazarus Demo . Collections/TCircle#Area().
//                   ^^^^^^ reference scip-pascal fpc rtl . System/Double#
      property Radius: Double read FRadius write FRadius;
//             ^^^^^^ definition scip-pascal lazarus Demo . Collections/TCircle#Radius.
//                    kind Property
//                    display_name Radius
//                    signature_documentation
//                    > property Radius: Double
//                     ^^^^^^ reference scip-pascal fpc rtl . System/Double#
//                                 ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#FRadius.
//                                               ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#FRadius.
    end;
//     ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TCircle#
  
    { Generic list with a default indexed property }
//          ⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TTypedList#
    generic TTypedList<T> = class
//          ^^^^^^^^^^ definition scip-pascal lazarus Demo . Collections/TTypedList#
//                     kind Class
//                     display_name TTypedList
//                     signature_documentation
//                     > type TTypedList<T> = class
//                     documentation
//                     > Generic list with a default indexed property
//                     ^ definition scip-pascal lazarus Demo . Collections/TTypedList#[T]
//                       kind TypeParameter
//                       display_name T
//                       signature_documentation
//                       > T
    private
      FItems: array of T;
//    ^^^^^^ definition scip-pascal lazarus Demo . Collections/TTypedList#FItems.
//           kind Field
//           display_name FItems
//           signature_documentation
//           > FItems: array of T
//                     ^ reference scip-pascal lazarus Demo . Collections/TTypedList#[T]
      FCount: Integer;
//    ^^^^^^ definition scip-pascal lazarus Demo . Collections/TTypedList#FCount.
//           kind Field
//           display_name FCount
//           signature_documentation
//           > FCount: Integer
//            ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
      function GetItem(Index: Integer): T;
//             ^^^^^^^ forward_definition scip-pascal lazarus Demo . Collections/TTypedList#GetItem().
//                     ^^^^^ definition local 5
//                           kind Parameter
//                           display_name Index
//                           signature_documentation
//                           > Index: Integer
//                            ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
//                                      ^ reference scip-pascal lazarus Demo . Collections/TTypedList#[T]
    public
      procedure Add(const AItem: T);
//              ^^^ forward_definition scip-pascal lazarus Demo . Collections/TTypedList#Add().
//                        ^^^^^ definition local 6
//                              kind Parameter
//                              display_name AItem
//                              signature_documentation
//                              > const AItem: T
//                               ^ reference scip-pascal lazarus Demo . Collections/TTypedList#[T]
      function IndexOf(const AItem: T): Integer;
//             ^^^^^^^ forward_definition scip-pascal lazarus Demo . Collections/TTypedList#IndexOf().
//                           ^^^^^ definition local 7
//                                 kind Parameter
//                                 display_name AItem
//                                 signature_documentation
//                                 > const AItem: T
//                                  ^ reference scip-pascal lazarus Demo . Collections/TTypedList#[T]
//                                      ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
      property Items[Index: Integer]: T read GetItem; default;
//             ^^^^^ definition scip-pascal lazarus Demo . Collections/TTypedList#Items.
//                   kind Property
//                   display_name Items
//                   signature_documentation
//                   > property Items[Index: Integer]: T
//                   ^^^^^ definition local 8
//                         kind Parameter
//                         display_name Index
//                         signature_documentation
//                         > Index: Integer
//                          ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
//                                    ^ reference scip-pascal lazarus Demo . Collections/TTypedList#[T]
//                                           ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#GetItem().
      property Count: Integer read FCount;
//             ^^^^^ definition scip-pascal lazarus Demo . Collections/TTypedList#Count.
//                   kind Property
//                   display_name Count
//                   signature_documentation
//                   > property Count: Integer
//                    ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
//                                 ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FCount.
    end;
//     ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TTypedList#
  
//  ⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TShapeList#
    TShapeList = specialize TTypedList<TShape>;
//  ^^^^^^^^^^ definition scip-pascal lazarus Demo . Collections/TShapeList#
//             kind Class
//             display_name TShapeList
//             signature_documentation
//             > type TShapeList = specialize TTypedList<TShape>
//                          ^^^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#
//                                     ^^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#
//                                            ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TShapeList#
  
//  ⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TShapeHelper#
    TShapeHelper = class helper for TShape
//  ^^^^^^^^^^^^ definition scip-pascal lazarus Demo . Collections/TShapeHelper#
//               kind Extension
//               display_name TShapeHelper
//               signature_documentation
//               > type TShapeHelper = class helper for TShape
//               relationship scip-pascal lazarus Demo . Collections/TShape# reference
//                                  ^^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#
      function IsBig: Boolean;
//             ^^^^^ forward_definition scip-pascal lazarus Demo . Collections/TShapeHelper#IsBig().
//                    ^^^^^^^ reference scip-pascal fpc rtl . System/Boolean#
    end;
//     ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TShapeHelper#
  
  operator + (const A, B: TVector) R: TVector;
//         ^ forward_definition scip-pascal lazarus Demo . Collections/+().
//                  ^ definition local 9
//                    kind Parameter
//                    display_name A
//                    signature_documentation
//                    > const A: TVector
//                     ^ definition local 10
//                       kind Parameter
//                       display_name B
//                       signature_documentation
//                       > const B: TVector
//                        ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
//                                 ^ definition local 11
//                                   kind Variable
//                                   display_name R
//                                   signature_documentation
//                                   > R: result
//                                    ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
  function Max(A, B: Integer): Integer; overload;
//         ^^^ forward_definition scip-pascal lazarus Demo . Collections/Max().
//             ^ definition local 12
//               kind Parameter
//               display_name A
//               signature_documentation
//               > A: Integer
//                ^ definition local 13
//                  kind Parameter
//                  display_name B
//                  signature_documentation
//                  > B: Integer
//                   ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
//                             ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  function Max(A, B: Double): Double; overload;
//         ^^^ forward_definition scip-pascal lazarus Demo . Collections/Max(+1).
//             ^ definition local 14
//               kind Parameter
//               display_name A
//               signature_documentation
//               > A: Double
//                ^ definition local 15
//                  kind Parameter
//                  display_name B
//                  signature_documentation
//                  > B: Double
//                   ^^^^^^ reference scip-pascal fpc rtl . System/Double#
//                            ^^^^^^ reference scip-pascal fpc rtl . System/Double#
  
  implementation
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TVector#Length().
  function TVector.Length: Double;
//         ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
//                 ^^^^^^ definition scip-pascal lazarus Demo . Collections/TVector#Length().
//                        kind Method
//                        display_name Length
//                        signature_documentation
//                        > function Length: Double
//                         ^^^^^^ reference scip-pascal fpc rtl . System/Double#
  begin
    Result := Sqrt(X * X + Y * Y);
//  ^^^^^^ reference local 16
//            ^^^^ reference scip-pascal fpc rtl . System/Sqrt().
//                 ^ reference scip-pascal lazarus Demo . Collections/TVector#X.
//                     ^ reference scip-pascal lazarus Demo . Collections/TVector#X.
//                         ^ reference scip-pascal lazarus Demo . Collections/TVector#Y.
//                             ^ reference scip-pascal lazarus Demo . Collections/TVector#Y.
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TVector#Length().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TVector#Make().
  class function TVector.Make(AX, AY: Double): TVector;
//               ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
//                       ^^^^ definition scip-pascal lazarus Demo . Collections/TVector#Make().
//                            kind StaticMethod
//                            display_name Make
//                            signature_documentation
//                            > class function Make(AX, AY: Double): TVector
//                            ^^ definition local 0
//                               kind Parameter
//                               display_name AX
//                               signature_documentation
//                               > AX: Double
//                                ^^ definition local 1
//                                   kind Parameter
//                                   display_name AY
//                                   signature_documentation
//                                   > AY: Double
//                                    ^^^^^^ reference scip-pascal fpc rtl . System/Double#
//                                             ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
  begin
    Result.X := AX;
//  ^^^^^^ reference local 17
//         ^ reference scip-pascal lazarus Demo . Collections/TVector#X.
//              ^^ reference local 0
    Result.Y := AY;
//  ^^^^^^ reference local 17
//         ^ reference scip-pascal lazarus Demo . Collections/TVector#Y.
//              ^^ reference local 1
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TVector#Make().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TShape#SetName().
  procedure TShape.SetName(const AValue: string);
//          ^^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#
//                 ^^^^^^^ definition scip-pascal lazarus Demo . Collections/TShape#SetName().
//                         kind Method
//                         display_name SetName
//                         signature_documentation
//                         > procedure SetName(const AValue: string)
//                               ^^^^^^ definition local 2
//                                      kind Parameter
//                                      display_name AValue
//                                      signature_documentation
//                                      > const AValue: string
  begin
    FName := Trim(AValue);
//  ^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#FName.
//           ^^^^ reference scip-pascal fpc rtl . SysUtils/Trim().
//                ^^^^^^ reference local 2
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TShape#SetName().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TShape#Create().
  constructor TShape.Create(const AName: string);
//            ^^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#
//                   ^^^^^^ definition scip-pascal lazarus Demo . Collections/TShape#Create().
//                          kind Constructor
//                          display_name Create
//                          signature_documentation
//                          > constructor Create(const AName: string)
//                                ^^^^^ definition local 3
//                                      kind Parameter
//                                      display_name AName
//                                      signature_documentation
//                                      > const AName: string
  begin
    inherited Create;
//            ^^^^^^ reference scip-pascal fpc rtl . System/TObject#Create().
    Name := AName;
//  ^^^^ reference scip-pascal lazarus Demo . Collections/TShape#Name.
//          ^^^^^ reference local 3
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TShape#Create().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TShape#Describe().
  function TShape.Describe: string;
//         ^^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#
//                ^^^^^^^^ definition scip-pascal lazarus Demo . Collections/TShape#Describe().
//                         kind Method
//                         display_name Describe
//                         signature_documentation
//                         > function Describe: string
//                         relationship scip-pascal lazarus Demo . Collections/IShape#Describe(). implementation
  begin
    Result := Format('%s (%.2f)', [FName, Area]);
//  ^^^^^^ reference local 18
//            ^^^^^^ reference scip-pascal fpc rtl . SysUtils/Format().
//                                 ^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#FName.
//                                        ^^^^ reference scip-pascal lazarus Demo . Collections/TShape#Area().
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TShape#Describe().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TCircle#Create().
  constructor TCircle.Create(const AName: string);
//            ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#
//                    ^^^^^^ definition scip-pascal lazarus Demo . Collections/TCircle#Create().
//                           kind Constructor
//                           display_name Create
//                           signature_documentation
//                           > constructor Create(const AName: string)
//                           relationship scip-pascal lazarus Demo . Collections/TShape#Create(). implementation
//                                 ^^^^^ definition local 4
//                                       kind Parameter
//                                       display_name AName
//                                       signature_documentation
//                                       > const AName: string
  begin
    inherited Create(AName);
//            ^^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#Create().
//                   ^^^^^ reference local 4
    FKind := skCircle;
//  ^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#FKind.
//           ^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TShapeKind#skCircle.
    FRadius := 1.0;
//  ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#FRadius.
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TCircle#Create().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TCircle#Area().
  function TCircle.Area: Double;
//         ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#
//                 ^^^^ definition scip-pascal lazarus Demo . Collections/TCircle#Area().
//                      kind Method
//                      display_name Area
//                      signature_documentation
//                      > function Area: Double
//                      relationship scip-pascal lazarus Demo . Collections/IShape#Area(). implementation
//                      relationship scip-pascal lazarus Demo . Collections/TShape#Area(). implementation
//                       ^^^^^^ reference scip-pascal fpc rtl . System/Double#
  begin
    Result := Pi * Sqr(FRadius);
//  ^^^^^^ reference local 19
//            ^^ reference scip-pascal fpc rtl . System/Pi().
//                 ^^^ reference scip-pascal fpc rtl . System/Sqr().
//                     ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#FRadius.
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TCircle#Area().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TTypedList#GetItem().
  function TTypedList.GetItem(Index: Integer): T;
//         ^^^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#
//                    ^^^^^^^ definition scip-pascal lazarus Demo . Collections/TTypedList#GetItem().
//                            kind Method
//                            display_name GetItem
//                            signature_documentation
//                            > function GetItem(Index: Integer): T
//                            ^^^^^ definition local 5
//                                  kind Parameter
//                                  display_name Index
//                                  signature_documentation
//                                  > Index: Integer
//                                   ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
//                                             ^ reference scip-pascal lazarus Demo . Collections/TTypedList#[T]
  begin
    if (Index < 0) or (Index >= FCount) then
//      ^^^^^ reference local 5
//                     ^^^^^ reference local 5
//                              ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FCount.
      raise EArgumentOutOfRangeException.CreateFmt('Index %d out of range', [Index]);
//          ^^^^^^^^^^^^^^^^^^^^^^^^^^^^ reference scip-pascal fpc rtl . SysUtils/EArgumentOutOfRangeException#
//                                       ^^^^^^^^^ reference scip-pascal fpc rtl . SysUtils/Exception#CreateFmt().
//                                                                           ^^^^^ reference local 5
    Result := FItems[Index];
//  ^^^^^^ reference local 20
//            ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FItems.
//                   ^^^^^ reference local 5
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TTypedList#GetItem().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TTypedList#Add().
  procedure TTypedList.Add(const AItem: T);
//          ^^^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#
//                     ^^^ definition scip-pascal lazarus Demo . Collections/TTypedList#Add().
//                         kind Method
//                         display_name Add
//                         signature_documentation
//                         > procedure Add(const AItem: T)
//                               ^^^^^ definition local 6
//                                     kind Parameter
//                                     display_name AItem
//                                     signature_documentation
//                                     > const AItem: T
//                                      ^ reference scip-pascal lazarus Demo . Collections/TTypedList#[T]
  begin
    if FCount = System.Length(FItems) then
//     ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FCount.
//              ^^^^^^ reference scip-pascal fpc rtl . System/
//                     ^^^^^^ reference scip-pascal fpc rtl . System/Length().
//                            ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FItems.
      SetLength(FItems, FCount * 2 + 4);
//    ^^^^^^^^^ reference scip-pascal fpc rtl . System/SetLength().
//              ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FItems.
//                      ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FCount.
    FItems[FCount] := AItem;
//  ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FItems.
//         ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FCount.
//                    ^^^^^ reference local 6
    Inc(FCount);
//  ^^^ reference scip-pascal fpc rtl . System/Inc().
//      ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FCount.
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TTypedList#Add().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TTypedList#IndexOf().
  function TTypedList.IndexOf(const AItem: T): Integer;
//         ^^^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#
//                    ^^^^^^^ definition scip-pascal lazarus Demo . Collections/TTypedList#IndexOf().
//                            kind Method
//                            display_name IndexOf
//                            signature_documentation
//                            > function IndexOf(const AItem: T): Integer
//                                  ^^^^^ definition local 7
//                                        kind Parameter
//                                        display_name AItem
//                                        signature_documentation
//                                        > const AItem: T
//                                         ^ reference scip-pascal lazarus Demo . Collections/TTypedList#[T]
//                                             ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  var
    I: Integer;
//  ^ definition local 21
//    kind Variable
//    display_name I
//    signature_documentation
//    > var I: Integer
//     ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  begin
    for I := 0 to FCount - 1 do
//      ^ reference local 21
//                ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FCount.
      if FItems[I] = AItem then
//       ^^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#FItems.
//              ^ reference local 21
//                   ^^^^^ reference local 7
        Exit(I);
//      ^^^^ reference scip-pascal fpc rtl . System/Exit().
//           ^ reference local 21
    Result := -1;
//  ^^^^^^ reference local 22
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TTypedList#IndexOf().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/TShapeHelper#IsBig().
  function TShapeHelper.IsBig: Boolean;
//         ^^^^^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TShapeHelper#
//                      ^^^^^ definition scip-pascal lazarus Demo . Collections/TShapeHelper#IsBig().
//                            kind Method
//                            display_name IsBig
//                            signature_documentation
//                            > function IsBig: Boolean
//                             ^^^^^^^ reference scip-pascal fpc rtl . System/Boolean#
  begin
    Result := Area > 100;
//  ^^^^^^ reference local 23
//            ^^^^ reference scip-pascal lazarus Demo . Collections/TShape#Area().
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/TShapeHelper#IsBig().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/+().
  operator + (const A, B: TVector) R: TVector;
//         ^ definition scip-pascal lazarus Demo . Collections/+().
//           kind Operator
//           display_name +
//           signature_documentation
//           > operator + (const A, B: TVector) R: TVector
//                  ^ definition local 9
//                    kind Parameter
//                    display_name A
//                    signature_documentation
//                    > const A: TVector
//                     ^ definition local 10
//                       kind Parameter
//                       display_name B
//                       signature_documentation
//                       > const B: TVector
//                        ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
//                                 ^ definition local 24
//                                   kind Variable
//                                   display_name R
//                                   signature_documentation
//                                   > R: result
//                                    ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
  begin
    R.X := A.X + B.X;
//  ^ reference local 24
//    ^ reference scip-pascal lazarus Demo . Collections/TVector#X.
//         ^ reference local 9
//           ^ reference scip-pascal lazarus Demo . Collections/TVector#X.
//               ^ reference local 10
//                 ^ reference scip-pascal lazarus Demo . Collections/TVector#X.
    R.Y := A.Y + B.Y;
//  ^ reference local 24
//    ^ reference scip-pascal lazarus Demo . Collections/TVector#Y.
//         ^ reference local 9
//           ^ reference scip-pascal lazarus Demo . Collections/TVector#Y.
//               ^ reference local 10
//                 ^ reference scip-pascal lazarus Demo . Collections/TVector#Y.
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/+().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/Max().
  function Max(A, B: Integer): Integer;
//         ^^^ definition scip-pascal lazarus Demo . Collections/Max().
//             kind Function
//             display_name Max
//             signature_documentation
//             > function Max(A, B: Integer): Integer
//             ^ definition local 12
//               kind Parameter
//               display_name A
//               signature_documentation
//               > A: Integer
//                ^ definition local 13
//                  kind Parameter
//                  display_name B
//                  signature_documentation
//                  > B: Integer
//                   ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
//                             ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  begin
    if A > B then Result := A else Result := B;
//     ^ reference local 12
//         ^ reference local 13
//                ^^^^^^ reference local 25
//                          ^ reference local 12
//                                 ^^^^^^ reference local 25
//                                           ^ reference local 13
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/Max().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . Collections/Max(+1).
  function Max(A, B: Double): Double;
//         ^^^ definition scip-pascal lazarus Demo . Collections/Max(+1).
//             kind Function
//             display_name Max
//             signature_documentation
//             > function Max(A, B: Double): Double
//             ^ definition local 14
//               kind Parameter
//               display_name A
//               signature_documentation
//               > A: Double
//                ^ definition local 15
//                  kind Parameter
//                  display_name B
//                  signature_documentation
//                  > B: Double
//                   ^^^^^^ reference scip-pascal fpc rtl . System/Double#
//                            ^^^^^^ reference scip-pascal fpc rtl . System/Double#
  begin
    if A > B then Result := A else Result := B;
//     ^ reference local 14
//         ^ reference local 15
//                ^^^^^^ reference local 26
//                          ^ reference local 14
//                                 ^^^^^^ reference local 26
//                                           ^ reference local 15
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . Collections/Max(+1).
  
  end.
  
