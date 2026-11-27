//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/
  unit MainForm;
//     ^^^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/
//              kind Module
//              display_name MainForm
//              signature_documentation
//              > unit MainForm
  
  interface
  
  uses
    Winapi.Windows, System.Classes, Vcl.Forms, Vcl.Controls, Vcl.StdCtrls, Vcl.ExtCtrls, Shapes;
//  ^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . Winapi/Windows/
//                  ^^^^^^^^^^^^^^ reference scip-delphi delphi rtl . System/Classes/
//                                  ^^^^^^^^^ reference scip-delphi delphi vcl . Vcl/Forms/
//                                             ^^^^^^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/
//                                                           ^^^^^^^^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/
//                                                                         ^^^^^^^^^^^^ reference scip-delphi delphi vcl . Vcl/ExtCtrls/
//                                                                                       ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/
  
  type
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/TFormMain#
    TFormMain = class(TForm)
//  ^^^^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/TFormMain#
//            kind Class
//            display_name TFormMain
//            signature_documentation
//            > type TFormMain = class(TForm)
//            relationship scip-delphi delphi vcl . Vcl/Forms/TForm# implementation
//                    ^^^^^ reference scip-delphi delphi vcl . Vcl/Forms/TForm#
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/TFormMain#PanelTop.
      PanelTop: TPanel;
//    ^^^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/TFormMain#PanelTop.
//             kind Field
//             display_name PanelTop
//             signature_documentation
//             > PanelTop: TPanel
//              ^^^^^^ reference scip-delphi delphi vcl . Vcl/ExtCtrls/TPanel#
//                    ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/TFormMain#PanelTop.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDraw.
      ButtonDraw: TButton;
//    ^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDraw.
//               kind Field
//               display_name ButtonDraw
//               signature_documentation
//               > ButtonDraw: TButton
//                ^^^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/TButton#
//                       ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDraw.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/TFormMain#EditName.
      EditName: TEdit;
//    ^^^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/TFormMain#EditName.
//             kind Field
//             display_name EditName
//             signature_documentation
//             > EditName: TEdit
//              ^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/TEdit#
//                   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/TFormMain#EditName.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/TFormMain#MemoLog.
      MemoLog: TMemo;
//    ^^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/TFormMain#MemoLog.
//            kind Field
//            display_name MemoLog
//            signature_documentation
//            > MemoLog: TMemo
//             ^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/TMemo#
//                  ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/TFormMain#MemoLog.
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDrawClick().
      procedure ButtonDrawClick(Sender: TObject);
//              ^^^^^^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDrawClick().
//                              kind Method
//                              display_name ButtonDrawClick
//                              signature_documentation
//                              > procedure ButtonDrawClick(Sender: TObject)
//                              ^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDrawClick().(Sender)
//                                     kind Parameter
//                                     display_name Sender
//                                     signature_documentation
//                                     > Sender: TObject
//                                      ^^^^^^^ reference scip-delphi delphi rtl . System/TObject#
//                                              ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDrawClick().
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/TFormMain#FormCreate().
      procedure FormCreate(Sender: TObject);
//              ^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/TFormMain#FormCreate().
//                         kind Method
//                         display_name FormCreate
//                         signature_documentation
//                         > procedure FormCreate(Sender: TObject)
//                         ^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/TFormMain#FormCreate().(Sender)
//                                kind Parameter
//                                display_name Sender
//                                signature_documentation
//                                > Sender: TObject
//                                 ^^^^^^^ reference scip-delphi delphi rtl . System/TObject#
//                                         ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/TFormMain#FormCreate().
    private
//    ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/TFormMain#FShape.
      FShape: TShape;
//    ^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/TFormMain#FShape.
//           kind Field
//           display_name FShape
//           signature_documentation
//           > FShape: TShape
//            ^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#
//                  ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/TFormMain#FShape.
    end;
//     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/TFormMain#
  
  var
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/FormMain.
    FormMain: TFormMain;
//  ^^^^^^^^ definition scip-delphi delphi fixture 1.0 MainForm/FormMain.
//           kind Variable
//           display_name FormMain
//           signature_documentation
//           > var FormMain: TFormMain
//            ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#
//                     ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/FormMain.
  
  implementation
  
  {$R *.dfm}
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/TFormMain#FormCreate().
  procedure TFormMain.FormCreate(Sender: TObject);
//          ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#
//                    ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#FormCreate().
//                               ^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#FormCreate().(Sender)
//                                       ^^^^^^^ reference scip-delphi delphi rtl . System/TObject#
  begin
    FShape := MakeCircle(2.0);
//  ^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#FShape.
//            ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Shapes/MakeCircle().
    Caption := 'Shapes';
//  ^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Caption.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/TFormMain#FormCreate().
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDrawClick().
  procedure TFormMain.ButtonDrawClick(Sender: TObject);
//          ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#
//                    ^^^^^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDrawClick().
//                                    ^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDrawClick().(Sender)
//                                            ^^^^^^^ reference scip-delphi delphi rtl . System/TObject#
  begin
    FShape.Name := EditName.Text;
//  ^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#FShape.
//         ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Name.
//                 ^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#EditName.
//                          ^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Text.
    FShape.Draw(MemoLog.Lines);
//  ^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#FShape.
//         ^^^^ reference scip-delphi delphi fixture 1.0 Shapes/TShape#Draw().
//              ^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#MemoLog.
//                      ^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/TCustomMemo#Lines.
    MemoLog.Lines.Add(IntToStr(MemoLog.Lines.Count));
//  ^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#MemoLog.
//          ^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/TCustomMemo#Lines.
//                ^^^ reference scip-delphi delphi rtl . System/Classes/TStrings#Add().
//                    ^^^^^^^^ reference scip-delphi delphi vcl . Vcl/Forms/TForm#IntToStr().
//                             ^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#MemoLog.
//                                     ^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/TCustomMemo#Lines.
//                                           ^^^^^ reference scip-delphi delphi rtl . System/Classes/TStrings#Count.
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDrawClick().
  
  end.
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 MainForm/
  
