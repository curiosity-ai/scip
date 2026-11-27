//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/
  unit Shapes;
//     ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/
//            kind Module
//            display_name Shapes
//            signature_documentation
//            > unit Shapes
  
  {$SCOPEDENUMS ON}
  
  interface
  
  uses
    System.SysUtils, System.Classes, System.Generics.Collections;
//  ^^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/
//                   ^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/Classes/
//                                   ^^^^^^^^^^^^^^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/Generics/Collections/
  
  type
    /// <summary>Colours a shape can have.</summary>
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeColor#
    TShapeColor = (Red, Green, Blue);
//  ^^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeColor#
//              kind Enum
//              display_name TShapeColor
//              signature_documentation
//              > type TShapeColor = (Red, Green, Blue)
//              documentation
//              > Colours a shape can have.
//                 ^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeColor#Red.
//                     kind EnumMember
//                     display_name Red
//                     signature_documentation
//                     > TShapeColor.Red
//                      ^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeColor#Green.
//                            kind EnumMember
//                            display_name Green
//                            signature_documentation
//                            > TShapeColor.Green
//                             ^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeColor#Blue.
//                                  kind EnumMember
//                                  display_name Blue
//                                  signature_documentation
//                                  > TShapeColor.Blue
//                                  ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeColor#
  
  {$SCOPEDENUMS OFF}
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeKind#
    TShapeKind = (skCircle, skSquare);
//  ^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeKind#
//             kind Enum
//             display_name TShapeKind
//             signature_documentation
//             > type TShapeKind = (skCircle, skSquare)
//                ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeKind#skCircle.
//                         kind EnumMember
//                         display_name skCircle
//                         signature_documentation
//                         > TShapeKind.skCircle
//                          ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeKind#skSquare.
//                                   kind EnumMember
//                                   display_name skSquare
//                                   signature_documentation
//                                   > TShapeKind.skSquare
//                                   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeKind#
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeKinds#
    TShapeKinds = set of TShapeKind;
//  ^^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeKinds#
//              kind TypeAlias
//              display_name TShapeKinds
//              signature_documentation
//              > type TShapeKinds = set of TShapeKind
//                       ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeKind#
//                                 ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeKinds#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#
    TShape = class;
//  ^^^^^^ forward_definition scip-delphi delphi fixture 1.0 Shapes/TShape#
//                ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#
  
    /// <summary>Something that can be drawn.</summary>
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/IDrawable#
    IDrawable = interface
//  ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/IDrawable#
//            kind Interface
//            display_name IDrawable
//            signature_documentation
//            > type IDrawable = interface
//            documentation
//            > Something that can be drawn.
      ['{6A1F5B2C-3D4E-4F60-8A9B-0C1D2E3F4A5B}']
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/IDrawable#Draw().
      procedure Draw(const Canvas: TStrings);
//              ^^^^ definition scip-delphi delphi fixture 1.0 Shapes/IDrawable#Draw().
//                   kind Method
//                   display_name Draw
//                   signature_documentation
//                   > procedure Draw(const Canvas: TStrings)
//                         ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/IDrawable#Draw().(Canvas)
//                                kind Parameter
//                                display_name Canvas
//                                signature_documentation
//                                > const Canvas: TStrings
//                                 ^^^^^^^^ reference scip-delphi delphi rtl . System/Classes/TStrings#
//                                          ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/IDrawable#Draw().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/IDrawable#GetName().
      function GetName: string;
//             ^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/IDrawable#GetName().
//                     kind Method
//                     display_name GetName
//                     signature_documentation
//                     > function GetName: string
//                      ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                            ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/IDrawable#GetName().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/IDrawable#Name.
      property Name: string read GetName;
//             ^^^^ definition scip-delphi delphi fixture 1.0 Shapes/IDrawable#Name.
//                  kind Property
//                  display_name Name
//                  signature_documentation
//                  > property Name: string read GetName
//                   ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                               ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/IDrawable#GetName().
//                                      ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/IDrawable#Name.
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/IDrawable#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeEvent#
    TShapeEvent = procedure(Sender: TShape) of object;
//  ^^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeEvent#
//              kind Delegate
//              display_name TShapeEvent
//              signature_documentation
//              > type TShapeEvent = procedure(Sender: TShape) of object
//                          ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeEvent#(Sender)
//                                 kind Parameter
//                                 display_name Sender
//                                 signature_documentation
//                                 > Sender: TShape
//                                  ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                                                   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeEvent#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#
    [Serializable]
    TShape = class abstract(TInterfacedObject, IDrawable)
//  ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#
//         kind Class
//         display_name TShape
//         signature_documentation
//         > type TShape = class abstract(TInterfacedObject, IDrawable)
//         relationship scip-delphi delphi fixture 1.0 Shapes/IDrawable# implementation
//         relationship scip-delphi delphi rtl . System/TInterfacedObject# implementation
//                          ^^^^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/TInterfacedObject#
//                                             ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/IDrawable#
    private
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#FName.
      FName: string;
//    ^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#FName.
//          kind Field
//          display_name FName
//          signature_documentation
//          > FName: string
//           ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                 ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#FName.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#FColor.
      FColor: TShapeColor;
//    ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#FColor.
//           kind Field
//           display_name FColor
//           signature_documentation
//           > FColor: TShapeColor
//            ^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeColor#
//                       ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#FColor.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#FOnChange.
      FOnChange: TShapeEvent;
//    ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#FOnChange.
//              kind Field
//              display_name FOnChange
//              signature_documentation
//              > FOnChange: TShapeEvent
//               ^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeEvent#
//                          ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#FOnChange.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#SetName().
      procedure SetName(const Value: string);
//              ^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#SetName().
//                      kind Method
//                      display_name SetName
//                      signature_documentation
//                      > procedure SetName(const Value: string)
//                            ^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#SetName().(Value)
//                                  kind Parameter
//                                  display_name Value
//                                  signature_documentation
//                                  > const Value: string
//                                   ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                          ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#SetName().
    protected
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#GetName().
      function GetName: string; virtual;
//             ^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#GetName().
//                     kind Method
//                     display_name GetName
//                     signature_documentation
//                     > function GetName: string; virtual
//                     relationship scip-delphi delphi fixture 1.0 Shapes/IDrawable#GetName(). implementation
//                      ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#GetName().
    public
      type
//      ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#
        TPoint = record
//      ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#
//             kind Struct
//             display_name TPoint
//             signature_documentation
//             > type TPoint = record
//        ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#X.
//        ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#Y.
          X, Y: Integer;
//        ^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#X.
//          kind Field
//          display_name X
//          signature_documentation
//          > X: Integer
//           ^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#Y.
//             kind Field
//             display_name Y
//             signature_documentation
//             > Y: Integer
//              ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#X.
//                     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#Y.
        end;
//         ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#
      const
//      ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#DefaultName.
        DefaultName = 'shape';
//      ^^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#DefaultName.
//                  kind Constant
//                  display_name DefaultName
//                  signature_documentation
//                  > const DefaultName = 'shape'
//                           ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#DefaultName.
//              ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Count.
      class var Count: Integer;
//              ^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Count.
//                    kind StaticField
//                    display_name Count
//                    signature_documentation
//                    > class var Count: Integer
//                     ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                            ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Count.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Create().
      constructor Create(const AName: string); virtual;
//                ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Create().
//                       kind Constructor
//                       display_name Create
//                       signature_documentation
//                       > constructor Create(const AName: string); virtual
//                             ^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Create().(AName)
//                                   kind Parameter
//                                   display_name AName
//                                   signature_documentation
//                                   > const AName: string
//                                    ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                                    ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Create().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Destroy().
      destructor Destroy; override;
//               ^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Destroy().
//                       kind Method
//                       display_name Destroy
//                       signature_documentation
//                       > destructor Destroy; override
//                       relationship scip-delphi delphi rtl . System/TObject#Destroy(). implementation reference
//                                ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Destroy().
      /// <summary>Area of the shape.</summary>
      /// <returns>The area in square units.</returns>
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Area().
      function Area: Double; virtual; abstract;
//             ^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Area().
//                  kind AbstractMethod
//                  display_name Area
//                  signature_documentation
//                  > function Area: Double; abstract; virtual
//                  documentation
//                  > Area of the shape.
//                  > 
//                  > Returns: The area in square units.
//                   ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                                            ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Area().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().
      procedure Draw(const Canvas: TStrings); virtual;
//              ^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().
//                   kind Method
//                   display_name Draw
//                   signature_documentation
//                   > procedure Draw(const Canvas: TStrings); virtual
//                   relationship scip-delphi delphi fixture 1.0 Shapes/IDrawable#Draw(). implementation
//                         ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().(Canvas)
//                                kind Parameter
//                                display_name Canvas
//                                signature_documentation
//                                > const Canvas: TStrings
//                                 ^^^^^^^^ reference scip-delphi delphi rtl . System/Classes/TStrings#
//                                                   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Move().
      procedure Move(DX, DY: Integer); overload;
//              ^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Move().
//                   kind Method
//                   display_name Move
//                   signature_documentation
//                   > procedure Move(DX, DY: Integer); overload
//                   ^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Move().(DX)
//                      kind Parameter
//                      display_name DX
//                      signature_documentation
//                      > DX: Integer
//                       ^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Move().(DY)
//                          kind Parameter
//                          display_name DY
//                          signature_documentation
//                          > DY: Integer
//                           ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//                                             ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Move().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Move(+1).
      procedure Move(const P: TPoint); overload;
//              ^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Move(+1).
//                   kind Method
//                   display_name Move
//                   signature_documentation
//                   > procedure Move(const P: TPoint); overload
//                         ^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Move(+1).(P)
//                           kind Parameter
//                           display_name P
//                           signature_documentation
//                           > const P: TPoint
//                            ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#
//                                             ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Move(+1).
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Describe().
      class function Describe(Shape: TShape): string; static;
//                   ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Describe().
//                            kind StaticMethod
//                            display_name Describe
//                            signature_documentation
//                            > class function Describe(Shape: TShape): string; static
//                            ^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Describe().(Shape)
//                                  kind Parameter
//                                  display_name Shape
//                                  signature_documentation
//                                  > Shape: TShape
//                                   ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                                            ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                                          ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Describe().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Name.
      property Name: string read GetName write SetName;
//             ^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Name.
//                  kind Property
//                  display_name Name
//                  signature_documentation
//                  > property Name: string read GetName write SetName
//                   ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                               ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#GetName().
//                                             ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#SetName().
//                                                    ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Name.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Color.
      property Color: TShapeColor read FColor write FColor default TShapeColor.Red;
//             ^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#Color.
//                   kind Property
//                   display_name Color
//                   signature_documentation
//                   > property Color: TShapeColor read FColor write FColor default TShapeColor.Red
//                    ^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeColor#
//                                     ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#FColor.
//                                                  ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#FColor.
//                                                                 ^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeColor#
//                                                                             ^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeColor#Red.
//                                                                                ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Color.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#OnChange.
      property OnChange: TShapeEvent read FOnChange write FOnChange;
//             ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShape#OnChange.
//                      kind Event
//                      display_name OnChange
//                      signature_documentation
//                      > property OnChange: TShapeEvent read FOnChange write FOnChange
//                       ^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeEvent#
//                                        ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#FOnChange.
//                                                        ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#FOnChange.
//                                                                 ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#OnChange.
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TCircle#
    TCircle = class(TShape)
//  ^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TCircle#
//          kind Class
//          display_name TCircle
//          signature_documentation
//          > type TCircle = class(TShape)
//          relationship scip-delphi delphi fixture 1.0 Shapes/TShape# implementation
//                  ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
    private
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TCircle#FRadius.
      FRadius: Double;
//    ^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TCircle#FRadius.
//            kind Field
//            display_name FRadius
//            signature_documentation
//            > FRadius: Double
//             ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TCircle#FRadius.
    public
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().
      constructor Create(const AName: string; ARadius: Double); reintroduce;
//                ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().
//                       kind Constructor
//                       display_name Create
//                       signature_documentation
//                       > constructor Create(const AName: string; ARadius: Double); reintroduce
//                             ^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().(AName)
//                                   kind Parameter
//                                   display_name AName
//                                   signature_documentation
//                                   > const AName: string
//                                    ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                            ^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().(ARadius)
//                                                    kind Parameter
//                                                    display_name ARadius
//                                                    signature_documentation
//                                                    > ARadius: Double
//                                                     ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                                                                         ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TCircle#Area().
      function Area: Double; override;
//             ^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TCircle#Area().
//                  kind Method
//                  display_name Area
//                  signature_documentation
//                  > function Area: Double; override
//                  relationship scip-delphi delphi fixture 1.0 Shapes/TShape#Area(). implementation reference
//                   ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                                   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TCircle#Area().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TCircle#Radius.
      property Radius: Double read FRadius;
//             ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TCircle#Radius.
//                    kind Property
//                    display_name Radius
//                    signature_documentation
//                    > property Radius: Double read FRadius
//                     ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                                 ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#FRadius.
//                                        ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TCircle#Radius.
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TCircle#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeList#
    TShapeList<T: TShape> = class(TObjectList<T>)
//  ^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeList#
//             kind Class
//             display_name TShapeList
//             signature_documentation
//             > type TShapeList<T: TShape> = class(TObjectList<T>)
//             relationship scip-delphi delphi rtl . System/Generics/Collections/TObjectList# implementation
//             ^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeList#[T]
//               kind TypeParameter
//               display_name T
//               signature_documentation
//               > T
//                ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                                ^^^^^^^^^^^ reference scip-delphi delphi rtl . System/Generics/Collections/TObjectList#
//                                            ^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#[T]
    public
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeList#TotalArea().
      function TotalArea: Double;
//             ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeList#TotalArea().
//                       kind Method
//                       display_name TotalArea
//                       signature_documentation
//                       > function TotalArea: Double
//                        ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                              ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeList#TotalArea().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeList#Largest().
      function Largest: T;
//             ^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeList#Largest().
//                     kind Method
//                     display_name Largest
//                     signature_documentation
//                     > function Largest: T
//                      ^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#[T]
//                       ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeList#Largest().
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeList#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeHelper#
    TShapeHelper = class helper for TShape
//  ^^^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeHelper#
//               kind Extension
//               display_name TShapeHelper
//               signature_documentation
//               > type TShapeHelper = class helper for TShape
//                                  ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeHelper#IsBig().
      function IsBig: Boolean;
//             ^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TShapeHelper#IsBig().
//                   kind Method
//                   display_name IsBig
//                   signature_documentation
//                   > function IsBig: Boolean
//                    ^^^^^^^ reference scip-delphi delphi rtl . System/Boolean#
//                           ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeHelper#IsBig().
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeHelper#
  
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TKindHelper#
    TKindHelper = record helper for TShapeKind
//  ^^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TKindHelper#
//              kind Extension
//              display_name TKindHelper
//              signature_documentation
//              > type TKindHelper = record helper for TShapeKind
//                                  ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeKind#
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TKindHelper#ToString().
      function ToString: string;
//             ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/TKindHelper#ToString().
//                      kind Method
//                      display_name ToString
//                      signature_documentation
//                      > function ToString: string
//                       ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                             ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TKindHelper#ToString().
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TKindHelper#
  
  resourcestring
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/SUnnamed.
    SUnnamed = 'unnamed';
//  ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/SUnnamed.
//           kind Constant
//           display_name SUnnamed
//           signature_documentation
//           > resourcestring SUnnamed = 'unnamed'
//                      ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/SUnnamed.
  
  const
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/MaxShapes.
    MaxShapes = 100;
//  ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/MaxShapes.
//            kind Constant
//            display_name MaxShapes
//            signature_documentation
//            > const MaxShapes = 100
//                 ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/MaxShapes.
  
  var
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/Registry.
    Registry: TShapeList<TShape>;
//  ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/Registry.
//           kind Variable
//           display_name Registry
//           signature_documentation
//           > var Registry: TShapeList<TShape>
//            ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#
//                       ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                              ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/Registry.
  
  threadvar
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/LastError.
    LastError: string;
//  ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/LastError.
//            kind Variable
//            display_name LastError
//            signature_documentation
//            > var LastError: string
//             ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/LastError.
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/MakeCircle().
  function MakeCircle(Radius: Double): TCircle;
//         ^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/MakeCircle().
//                    kind Function
//                    display_name MakeCircle
//                    signature_documentation
//                    > function MakeCircle(Radius: Double): TCircle
//                    ^^^^^^ definition scip-delphi delphi fixture 1.0 Shapes/MakeCircle().(Radius)
//                           kind Parameter
//                           display_name Radius
//                           signature_documentation
//                           > Radius: Double
//                            ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                                     ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#
//                                            ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/MakeCircle().
  
  implementation
  
  uses
    System.Math;
//  ^^^^^^^^^^^ reference scip-delphi delphi rtl . System/Math/
  
  { TShape }
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Create().
  constructor TShape.Create(const AName: string);
//            ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                   ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Create().
//                                ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Create().(AName)
//                                       ^^^^^^ reference scip-delphi delphi rtl . System/string#
  begin
    inherited Create;
//            ^^^^^^ reference scip-delphi delphi rtl . System/TObject#Create().
    FName := AName;
//  ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#FName.
//           ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Create().(AName)
    Inc(Count);
//  ^^^ reference scip-delphi delphi rtl . System/Inc().
//      ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Count.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Create().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Destroy().
  destructor TShape.Destroy;
//           ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                  ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Destroy().
  begin
    Dec(Count);
//  ^^^ reference scip-delphi delphi rtl . System/Dec().
//      ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Count.
    inherited;
//  ^^^^^^^^^ reference scip-delphi delphi rtl . System/TObject#Destroy().
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Destroy().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#GetName().
  function TShape.GetName: string;
//         ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#GetName().
//                         ^^^^^^ reference scip-delphi delphi rtl . System/string#
  begin
    if FName = '' then
//     ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#FName.
      Result := SUnnamed
//              ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/SUnnamed.
    else
      Result := FName;
//              ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#FName.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#GetName().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#SetName().
  procedure TShape.SetName(const Value: string);
//          ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                 ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#SetName().
//                               ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#SetName().(Value)
//                                      ^^^^^^ reference scip-delphi delphi rtl . System/string#
  begin
    FName := Value;
//  ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#FName.
//           ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#SetName().(Value)
    if Assigned(FOnChange) then
//     ^^^^^^^^ reference scip-delphi delphi rtl . System/Assigned().
//              ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#FOnChange.
      FOnChange(Self);
//    ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#FOnChange.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#SetName().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().
  procedure TShape.Draw(const Canvas: TStrings);
//          ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                 ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().
//                            ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().(Canvas)
//                                    ^^^^^^^^ reference scip-delphi delphi rtl . System/Classes/TStrings#
  begin
    Canvas.Add(Format('%s: %.2f', [Name, Area]));
//  ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().(Canvas)
//         ^^^ reference scip-delphi delphi rtl . System/Classes/TStrings#Add().
//             ^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/Format().
//                                 ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Name.
//                                       ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Area().
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Move().
  procedure TShape.Move(DX, DY: Integer);
//          ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                 ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Move().
//                      ^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Move().(DX)
//                          ^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Move().(DY)
//                              ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
  begin
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Move().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Move(+1).
  procedure TShape.Move(const P: TPoint);
//          ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                 ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Move(+1).
//                            ^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Move(+1).(P)
//                               ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#
  begin
    Move(P.X, P.Y);
//  ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Move().
//       ^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Move(+1).(P)
//         ^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#X.
//            ^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Move(+1).(P)
//              ^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#TPoint#Y.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Move(+1).
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShape#Describe().
  class function TShape.Describe(Shape: TShape): string;
//               ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                      ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Describe().
//                               ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Describe().(Shape)
//                                      ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                                               ^^^^^^ reference scip-delphi delphi rtl . System/string#
  begin
    Result := Shape.Name + ' ' + Shape.ClassName;
//            ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Describe().(Shape)
//                  ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Name.
//                               ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Describe().(Shape)
//                                     ^^^^^^^^^ reference scip-delphi delphi rtl . System/TObject#ClassName().
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShape#Describe().
  
  { TCircle }
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().
  constructor TCircle.Create(const AName: string; ARadius: Double);
//            ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#
//                    ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().
//                                 ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().(AName)
//                                        ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                                ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().(ARadius)
//                                                         ^^^^^^ reference scip-delphi delphi rtl . System/Double#
  begin
    inherited Create(AName);
//            ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Create().
//                   ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().(AName)
    FRadius := ARadius;
//  ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#FRadius.
//             ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().(ARadius)
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TCircle#Area().
  function TCircle.Area: Double;
//         ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#
//                 ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#Area().
//                       ^^^^^^ reference scip-delphi delphi rtl . System/Double#
  begin
    Result := Pi * Sqr(FRadius);
//            ^^ reference scip-delphi delphi rtl . System/Pi().
//                 ^^^ reference scip-delphi delphi rtl . System/Sqr().
//                     ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#FRadius.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TCircle#Area().
  
  { TShapeList<T> }
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeList#TotalArea().
  function TShapeList<T>.TotalArea: Double;
//         ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#
//                    ^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#[T]
//                       ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#TotalArea().
//                                  ^^^^^^ reference scip-delphi delphi rtl . System/Double#
  begin
    Result := 0;
    for var S in Self do
//          ^ definition local 0
//            kind Variable
//            display_name S
//            signature_documentation
//            > S
      Result := Result + S.Area;
//                       ^ reference local 0
//                         ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Area().
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeList#TotalArea().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeList#Largest().
  function TShapeList<T>.Largest: T;
//         ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#
//                    ^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#[T]
//                       ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#Largest().
//                                ^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#[T]
  var
//  ⌄ enclosing_range_start local 1
    I: Integer;
//  ^ definition local 1
//    kind Variable
//    display_name I
//    signature_documentation
//    > I: Integer
//     ^^^^^^^ reference scip-delphi delphi rtl . System/Integer#
//            ⌃ enclosing_range_end local 1
  begin
    Result := nil;
    for I := 0 to Count - 1 do
//      ^ reference local 1
//                ^^^^^ reference scip-delphi delphi rtl . System/Generics/Collections/TList#Count.
      if (Result = nil) or (Items[I].Area > Result.Area) then
//                          ^^^^^ reference scip-delphi delphi rtl . System/Generics/Collections/TList#Items.
//                                ^ reference local 1
//                                                 ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Area().
        Result := Items[I];
//                ^^^^^ reference scip-delphi delphi rtl . System/Generics/Collections/TList#Items.
//                      ^ reference local 1
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeList#Largest().
  
  { TShapeHelper }
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TShapeHelper#IsBig().
  function TShapeHelper.IsBig: Boolean;
//         ^^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeHelper#
//                      ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeHelper#IsBig().
//                             ^^^^^^^ reference scip-delphi delphi rtl . System/Boolean#
  begin
    Result := Area > 10;
//            ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Area().
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TShapeHelper#IsBig().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/TKindHelper#ToString().
  function TKindHelper.ToString: string;
//         ^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TKindHelper#
//                     ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TKindHelper#ToString().
//                               ^^^^^^ reference scip-delphi delphi rtl . System/string#
  begin
    case Self of
      skCircle: Result := 'circle';
//    ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeKind#skCircle.
      skSquare: Result := 'square';
//    ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeKind#skSquare.
    end;
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/TKindHelper#ToString().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Shapes/MakeCircle().
  function MakeCircle(Radius: Double): TCircle;
//         ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/MakeCircle().
//                    ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/MakeCircle().(Radius)
//                            ^^^^^^ reference scip-delphi delphi rtl . System/Double#
//                                     ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#
  begin
    Result := TCircle.Create(SUnnamed, Max(Radius, 0));
//            ^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#
//                    ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TCircle#Create().
//                           ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/SUnnamed.
//                                     ^^^ reference scip-delphi delphi rtl . System/Math/Max().
//                                         ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/MakeCircle().(Radius)
    with Result do
      Color := TShapeColor.Blue;
//    ^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Color.
//             ^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeColor#
//                         ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeColor#Blue.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/MakeCircle().
  
  initialization
    Registry := TShapeList<TShape>.Create;
//  ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/Registry.
//              ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShapeList#
//                         ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                                 ^^^^^^ reference scip-delphi delphi rtl . System/Generics/Collections/TObjectList#Create().
  
  finalization
    FreeAndNil(Registry);
//  ^^^^^^^^^^ reference scip-delphi delphi rtl . System/SysUtils/FreeAndNil().
//             ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/Registry.
  
  end.
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Shapes/
  
