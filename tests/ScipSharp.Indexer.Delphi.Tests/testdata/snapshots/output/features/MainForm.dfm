  object FormMain: TFormMain
//       ^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/FormMain.
//                 ^^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#
    Left = 0
//  ^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Left.
    Top = 0
//  ^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Top.
    Caption = 'Shapes'
//  ^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Caption.
    ClientHeight = 300
//  ^^^^^^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#ClientHeight.
    ClientWidth = 400
//  ^^^^^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#ClientWidth.
    OnCreate = FormCreate
//  ^^^^^^^^ reference scip-delphi delphi vcl . Vcl/Forms/TCustomForm#OnCreate.
//             ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#FormCreate().
    TextHeight = 15
    object PanelTop: TPanel
//         ^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#PanelTop.
//                   ^^^^^^ reference scip-delphi delphi vcl . Vcl/ExtCtrls/TPanel#
      Left = 0
//    ^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Left.
      Top = 0
//    ^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Top.
      Width = 400
//    ^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Width.
      Height = 41
//    ^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Height.
      Align = alTop
//    ^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Align.
      TabOrder = 0
//    ^^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TWinControl#TabOrder.
      object ButtonDraw: TButton
//           ^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDraw.
//                       ^^^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/TButton#
        Left = 8
//      ^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Left.
        Top = 8
//      ^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Top.
        Width = 75
//      ^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Width.
        Height = 25
//      ^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Height.
        Caption = 'Draw'
//      ^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Caption.
        TabOrder = 0
//      ^^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TWinControl#TabOrder.
        OnClick = ButtonDrawClick
//      ^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#OnClick.
//                ^^^^^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#ButtonDrawClick().
      end
      object EditName: TEdit
//           ^^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#EditName.
//                     ^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/TEdit#
        Left = 96
//      ^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Left.
        Top = 8
//      ^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Top.
        Width = 121
//      ^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Width.
        Height = 23
//      ^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Height.
        TabOrder = 1
//      ^^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TWinControl#TabOrder.
        Text = 'circle'
//      ^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Text.
      end
    end
    object MemoLog: TMemo
//         ^^^^^^^ reference scip-delphi delphi fixture 1.0 MainForm/TFormMain#MemoLog.
//                  ^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/TMemo#
      Left = 0
//    ^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Left.
      Top = 41
//    ^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Top.
      Width = 400
//    ^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Width.
      Height = 259
//    ^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Height.
      Align = alClient
//    ^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TControl#Align.
      Lines.Strings = (
//    ^^^^^ reference scip-delphi delphi vcl . Vcl/StdCtrls/TCustomMemo#Lines.
//          ^^^^^^^ reference scip-delphi delphi rtl . System/Classes/TStrings#Strings.
        'ready')
      TabOrder = 1
//    ^^^^^^^^ reference scip-delphi delphi vcl . Vcl/Controls/TWinControl#TabOrder.
    end
  end
  
