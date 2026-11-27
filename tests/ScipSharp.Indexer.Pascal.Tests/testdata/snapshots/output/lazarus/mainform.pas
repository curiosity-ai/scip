  unit MainForm;
//     ^^^^^^^^ definition scip-pascal lazarus Demo . MainForm/
//              kind Module
//              display_name MainForm
//              signature_documentation
//              > unit MainForm
  
  {$mode objfpc}{$H+}
  
  interface
  
  uses
    Classes, SysUtils, Forms, Controls, StdCtrls;
//  ^^^^^^^ reference scip-pascal fpc rtl . Classes/
//           ^^^^^^^^ reference scip-pascal fpc rtl . SysUtils/
//                     ^^^^^ reference scip-pascal lazarus lcl . Forms/
//                            ^^^^^^^^ reference scip-pascal lazarus lcl . Controls/
//                                      ^^^^^^^^ reference scip-pascal lazarus lcl . StdCtrls/
  
  type
    { TForm1 }
//  ⌄ enclosing_range_start scip-pascal lazarus Demo . MainForm/TForm1#
    TForm1 = class(TForm)
//  ^^^^^^ definition scip-pascal lazarus Demo . MainForm/TForm1#
//         kind Class
//         display_name TForm1
//         signature_documentation
//         > type TForm1 = class(TForm)
//         documentation
//         > TForm1
//         relationship scip-pascal lazarus lcl . Forms/TForm# implementation
//                 ^^^^^ reference scip-pascal lazarus lcl . Forms/TForm#
      Button1: TButton;
//    ^^^^^^^ definition scip-pascal lazarus Demo . MainForm/TForm1#Button1.
//            kind Field
//            display_name Button1
//            signature_documentation
//            > Button1: TButton
//             ^^^^^^^ reference scip-pascal lazarus lcl . StdCtrls/TButton#
      Memo1: TMemo;
//    ^^^^^ definition scip-pascal lazarus Demo . MainForm/TForm1#Memo1.
//          kind Field
//          display_name Memo1
//          signature_documentation
//          > Memo1: TMemo
//           ^^^^^ reference scip-pascal lazarus lcl . StdCtrls/TMemo#
      procedure Button1Click(Sender: TObject);
//              ^^^^^^^^^^^^ forward_definition scip-pascal lazarus Demo . MainForm/TForm1#Button1Click().
//                           ^^^^^^ definition local 0
//                                  kind Parameter
//                                  display_name Sender
//                                  signature_documentation
//                                  > Sender: TObject
//                                   ^^^^^^^ reference scip-pascal fpc rtl . System/TObject#
      procedure FormCreate(Sender: TObject);
//              ^^^^^^^^^^ forward_definition scip-pascal lazarus Demo . MainForm/TForm1#FormCreate().
//                         ^^^^^^ definition local 1
//                                kind Parameter
//                                display_name Sender
//                                signature_documentation
//                                > Sender: TObject
//                                 ^^^^^^^ reference scip-pascal fpc rtl . System/TObject#
    private
      FClicks: Integer;
//    ^^^^^^^ definition scip-pascal lazarus Demo . MainForm/TForm1#FClicks.
//            kind Field
//            display_name FClicks
//            signature_documentation
//            > FClicks: Integer
//             ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
    end;
//     ⌃ enclosing_range_end scip-pascal lazarus Demo . MainForm/TForm1#
  
  var
    Form1: TForm1;
//  ^^^^^ definition scip-pascal lazarus Demo . MainForm/Form1.
//        kind Variable
//        display_name Form1
//        signature_documentation
//        > var Form1: TForm1
//         ^^^^^^ reference scip-pascal lazarus Demo . MainForm/TForm1#
  
  implementation
  
  {$R *.lfm}
  
  { TForm1 }
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . MainForm/TForm1#FormCreate().
  procedure TForm1.FormCreate(Sender: TObject);
//          ^^^^^^ reference scip-pascal lazarus Demo . MainForm/TForm1#
//                 ^^^^^^^^^^ definition scip-pascal lazarus Demo . MainForm/TForm1#FormCreate().
//                            kind Method
//                            display_name FormCreate
//                            signature_documentation
//                            > procedure FormCreate(Sender: TObject)
//                            ^^^^^^ definition local 1
//                                   kind Parameter
//                                   display_name Sender
//                                   signature_documentation
//                                   > Sender: TObject
//                                    ^^^^^^^ reference scip-pascal fpc rtl . System/TObject#
  begin
    FClicks := 0;
//  ^^^^^^^ reference scip-pascal lazarus Demo . MainForm/TForm1#FClicks.
    Caption := 'Demo';
//  ^^^^^^^ reference scip-pascal lazarus lcl . Controls/TControl#Caption.
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . MainForm/TForm1#FormCreate().
  
//⌄ enclosing_range_start scip-pascal lazarus Demo . MainForm/TForm1#Button1Click().
  procedure TForm1.Button1Click(Sender: TObject);
//          ^^^^^^ reference scip-pascal lazarus Demo . MainForm/TForm1#
//                 ^^^^^^^^^^^^ definition scip-pascal lazarus Demo . MainForm/TForm1#Button1Click().
//                              kind Method
//                              display_name Button1Click
//                              signature_documentation
//                              > procedure Button1Click(Sender: TObject)
//                              ^^^^^^ definition local 0
//                                     kind Parameter
//                                     display_name Sender
//                                     signature_documentation
//                                     > Sender: TObject
//                                      ^^^^^^^ reference scip-pascal fpc rtl . System/TObject#
  begin
    Inc(FClicks);
//  ^^^ reference scip-pascal fpc rtl . System/Inc().
//      ^^^^^^^ reference scip-pascal lazarus Demo . MainForm/TForm1#FClicks.
    Memo1.Lines.Add(Format('Clicked %d times', [FClicks]));
//  ^^^^^ reference scip-pascal lazarus Demo . MainForm/TForm1#Memo1.
//        ^^^^^ reference scip-pascal lazarus lcl . StdCtrls/TMemo#Lines.
//              ^^^ reference scip-pascal fpc rtl . Classes/TStrings#Add().
//                  ^^^^^^ reference scip-pascal fpc rtl . SysUtils/Format().
//                                              ^^^^^^^ reference scip-pascal lazarus Demo . MainForm/TForm1#FClicks.
    Button1.Caption := IntToStr(FClicks);
//  ^^^^^^^ reference scip-pascal lazarus Demo . MainForm/TForm1#Button1.
//          ^^^^^^^ reference scip-pascal lazarus lcl . Controls/TControl#Caption.
//                     ^^^^^^^^ reference scip-pascal fpc rtl . SysUtils/IntToStr().
//                              ^^^^^^^ reference scip-pascal lazarus Demo . MainForm/TForm1#FClicks.
  end;
//  ⌃ enclosing_range_end scip-pascal lazarus Demo . MainForm/TForm1#Button1Click().
  
  end.
  
