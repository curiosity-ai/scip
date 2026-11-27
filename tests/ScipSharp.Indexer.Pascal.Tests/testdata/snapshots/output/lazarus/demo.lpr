  program Demo;
//        ^^^^ definition scip-pascal lazarus Demo . Demo/
//             kind Module
//             display_name Demo
//             signature_documentation
//             > program Demo
  
  {$mode objfpc}{$H+}
  
  uses
    {$IFDEF UNIX}cthreads,{$ENDIF}
//               ^^^^^^^^ reference scip-pascal fpc rtl . cthreads/
    Interfaces, Forms, SysUtils, Collections, MainForm;
//  ^^^^^^^^^^ reference scip-pascal lazarus lcl . Interfaces/
//              ^^^^^ reference scip-pascal lazarus lcl . Forms/
//                     ^^^^^^^^ reference scip-pascal fpc rtl . SysUtils/
//                               ^^^^^^^^^^^ reference scip-pascal lazarus Demo . Collections/
//                                            ^^^^^^^^ reference scip-pascal lazarus Demo . MainForm/
  
  var
    List: TShapeList;
//  ^^^^ definition scip-pascal lazarus Demo . Demo/List.
//       kind Variable
//       display_name List
//       signature_documentation
//       > var List: TShapeList
//        ^^^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TShapeList#
    Shape: TShape;
//  ^^^^^ definition scip-pascal lazarus Demo . Demo/Shape.
//        kind Variable
//        display_name Shape
//        signature_documentation
//        > var Shape: TShape
//         ^^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#
    V: TVector;
//  ^ definition scip-pascal lazarus Demo . Demo/V.
//    kind Variable
//    display_name V
//    signature_documentation
//    > var V: TVector
//     ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
    I: Integer;
//  ^ definition scip-pascal lazarus Demo . Demo/I.
//    kind Variable
//    display_name I
//    signature_documentation
//    > var I: Integer
//     ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  begin
    List := TShapeList.Create;
//  ^^^^ reference scip-pascal lazarus Demo . Demo/List.
//          ^^^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TShapeList#
//                     ^^^^^^ reference scip-pascal fpc rtl . System/TObject#Create().
    try
      List.Add(TCircle.Create('unit circle'));
//    ^^^^ reference scip-pascal lazarus Demo . Demo/List.
//         ^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#Add().
//             ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#
//                     ^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#Create().
      for I := 0 to List.Count - 1 do
//        ^ reference scip-pascal lazarus Demo . Demo/I.
//                  ^^^^ reference scip-pascal lazarus Demo . Demo/List.
//                       ^^^^^ reference scip-pascal lazarus Demo . Collections/TTypedList#Count.
      begin
        Shape := List[I];
//      ^^^^^ reference scip-pascal lazarus Demo . Demo/Shape.
//               ^^^^ reference scip-pascal lazarus Demo . Demo/List.
//                    ^ reference scip-pascal lazarus Demo . Demo/I.
        WriteLn(Shape.Describe, ' big=', Shape.IsBig);
//      ^^^^^^^ reference scip-pascal fpc rtl . System/WriteLn().
//              ^^^^^ reference scip-pascal lazarus Demo . Demo/Shape.
//                    ^^^^^^^^ reference scip-pascal lazarus Demo . Collections/TShape#Describe().
//                                       ^^^^^ reference scip-pascal lazarus Demo . Demo/Shape.
//                                             ^^^^^ reference scip-pascal lazarus Demo . Collections/TShapeHelper#IsBig().
        if Shape is TCircle then
//         ^^^^^ reference scip-pascal lazarus Demo . Demo/Shape.
//                  ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#
          WriteLn(TCircle(Shape).Radius:0:2);
//        ^^^^^^^ reference scip-pascal fpc rtl . System/WriteLn().
//                ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#
//                        ^^^^^ reference scip-pascal lazarus Demo . Demo/Shape.
//                               ^^^^^^ reference scip-pascal lazarus Demo . Collections/TCircle#Radius.
      end;
      V := TVector.Make(1, 2) + TVector.Make(3, 4);
//    ^ reference scip-pascal lazarus Demo . Demo/V.
//         ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
//                 ^^^^ reference scip-pascal lazarus Demo . Collections/TVector#Make().
//                            ^ reference scip-pascal lazarus Demo . Collections/+().
//                              ^^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#
//                                      ^^^^ reference scip-pascal lazarus Demo . Collections/TVector#Make().
      WriteLn(V.Length:0:3, ' ', Max(1, 2), ' ', Max(1.5, 2.5):0:1);
//    ^^^^^^^ reference scip-pascal fpc rtl . System/WriteLn().
//            ^ reference scip-pascal lazarus Demo . Demo/V.
//              ^^^^^^ reference scip-pascal lazarus Demo . Collections/TVector#Length().
//                               ^^^ reference scip-pascal lazarus Demo . Collections/Max().
//                                               ^^^ reference scip-pascal lazarus Demo . Collections/Max(+1).
    except
      on E: Exception do
//       ^ definition local 0
//         kind Variable
//         display_name E
//         signature_documentation
//         > E: Exception
//          ^^^^^^^^^ reference scip-pascal fpc rtl . SysUtils/Exception#
        WriteLn('error: ', E.Message);
//      ^^^^^^^ reference scip-pascal fpc rtl . System/WriteLn().
//                         ^ reference local 0
//                           ^^^^^^^ reference scip-pascal fpc rtl . SysUtils/Exception#Message.
    end;
    List.Free;
//  ^^^^ reference scip-pascal lazarus Demo . Demo/List.
//       ^^^^ reference scip-pascal fpc rtl . System/TObject#Free().
    RequireDerivedFormResource := True;
//  ^^^^^^^^^^^^^^^^^^^^^^^^^^ reference scip-pascal lazarus lcl . Forms/RequireDerivedFormResource.
//                                ^^^^ reference scip-pascal fpc rtl . System/True.
    Application.Initialize;
//  ^^^^^^^^^^^ reference scip-pascal lazarus lcl . Forms/Application.
//              ^^^^^^^^^^ reference scip-pascal lazarus lcl . Forms/TApplication#Initialize().
    Application.CreateForm(TForm1, Form1);
//  ^^^^^^^^^^^ reference scip-pascal lazarus lcl . Forms/Application.
//              ^^^^^^^^^^ reference scip-pascal lazarus lcl . Forms/TApplication#CreateForm().
//                         ^^^^^^ reference scip-pascal lazarus Demo . MainForm/TForm1#
//                                 ^^^^^ reference scip-pascal lazarus Demo . MainForm/Form1.
    Application.Run;
//  ^^^^^^^^^^^ reference scip-pascal lazarus lcl . Forms/Application.
//              ^^^ reference scip-pascal lazarus lcl . Forms/TApplication#Run().
  end.
  
