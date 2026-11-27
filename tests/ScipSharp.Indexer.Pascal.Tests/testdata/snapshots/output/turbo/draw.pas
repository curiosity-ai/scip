  program Draw;
//        ^^^^ definition scip-pascal . turbo . Draw/
//             kind Module
//             display_name Draw
//             signature_documentation
//             > program Draw
  
  uses Crt, Shapes;
//     ^^^ reference scip-pascal fpc rtl . Crt/
//          ^^^^^^ reference scip-pascal . turbo . Shapes/
  
  var
    C: PCircle;
//  ^ definition scip-pascal . turbo . Draw/C.
//    kind Variable
//    display_name C
//    signature_documentation
//    > var C: PCircle
//     ^^^^^^^ reference scip-pascal . turbo . Shapes/PCircle#
    S: TShape;
//  ^ definition scip-pascal . turbo . Draw/S.
//    kind Variable
//    display_name S
//    signature_documentation
//    > var S: TShape
//     ^^^^^^ reference scip-pascal . turbo . Shapes/TShape#
    Title: TName;
//  ^^^^^ definition scip-pascal . turbo . Draw/Title.
//        kind Variable
//        display_name Title
//        signature_documentation
//        > var Title: TName
//         ^^^^^ reference scip-pascal . turbo . Shapes/TName#
    Screen: array[1..25, 1..80] of Word absolute $B800:0000;
//  ^^^^^^ definition scip-pascal . turbo . Draw/Screen.
//         kind Variable
//         display_name Screen
//         signature_documentation
//         > var Screen: array[1..25, 1..80] of Word
//                                 ^^^^ reference scip-pascal fpc rtl . System/Word#
  
//⌄ enclosing_range_start scip-pascal . turbo . Draw/Pause().
  procedure Pause;
//          ^^^^^ definition scip-pascal . turbo . Draw/Pause().
//                kind Function
//                display_name Pause
//                signature_documentation
//                > procedure Pause
  var
    Ch: Char;
//  ^^ definition local 0
//     kind Variable
//     display_name Ch
//     signature_documentation
//     > var Ch: Char
//      ^^^^ reference scip-pascal fpc rtl . System/Char#
  begin
    Ch := ReadKey;
//  ^^ reference local 0
//        ^^^^^^^ reference scip-pascal fpc rtl . Crt/ReadKey().
    if Ch = #0 then
//     ^^ reference local 0
      Ch := ReadKey;
//    ^^ reference local 0
//          ^^^^^^^ reference scip-pascal fpc rtl . Crt/ReadKey().
  end;
//  ⌃ enclosing_range_end scip-pascal . turbo . Draw/Pause().
  
  begin
    ClrScr;
//  ^^^^^^ reference scip-pascal fpc rtl . Crt/ClrScr().
    New(C, Init(10, 10, 5));
//  ^^^ reference scip-pascal fpc rtl . System/New().
//      ^ reference scip-pascal . turbo . Draw/C.
//         ^^^^ reference scip-pascal . turbo . Shapes/TCircle#Init().
    C^.Draw;
//  ^ reference scip-pascal . turbo . Draw/C.
//     ^^^^ reference scip-pascal . turbo . Shapes/TCircle#Draw().
    S.Init(1, 1);
//  ^ reference scip-pascal . turbo . Draw/S.
//    ^^^^ reference scip-pascal . turbo . Shapes/TShape#Init().
    with S do
//       ^ reference scip-pascal . turbo . Draw/S.
    begin
      MoveTo(X + 1, Y + 1);
//    ^^^^^^ reference scip-pascal . turbo . Shapes/TShape#MoveTo().
//           ^ reference scip-pascal . turbo . Shapes/TShape#X.
//                  ^ reference scip-pascal . turbo . Shapes/TShape#Y.
      Done;
//    ^^^^ reference scip-pascal . turbo . Shapes/TShape#Done().
    end;
    Title := 'Circle';
//  ^^^^^ reference scip-pascal . turbo . Draw/Title.
    WriteLn(Title, ': ', Area(C^):0:2, Bell);
//  ^^^^^^^ reference scip-pascal fpc rtl . System/WriteLn().
//          ^^^^^ reference scip-pascal . turbo . Draw/Title.
//                       ^^^^ reference scip-pascal . turbo . Shapes/Area().
//                            ^ reference scip-pascal . turbo . Draw/C.
//                                     ^^^^ reference scip-pascal . turbo . Shapes/Bell.
    Dispose(C, Done);
//  ^^^^^^^ reference scip-pascal fpc rtl . System/Dispose().
//          ^ reference scip-pascal . turbo . Draw/C.
//             ^^^^ reference scip-pascal . turbo . Shapes/TShape#Done().
    Pause;
//  ^^^^^ reference scip-pascal . turbo . Draw/Pause().
    asm
      mov ax, 3
      int $10
    end;
  end.
  
