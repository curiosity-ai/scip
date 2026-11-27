object FormMain: TFormMain
  Left = 0
  Top = 0
  Caption = 'Shapes'
  ClientHeight = 300
  ClientWidth = 400
  OnCreate = FormCreate
  TextHeight = 15
  object PanelTop: TPanel
    Left = 0
    Top = 0
    Width = 400
    Height = 41
    Align = alTop
    TabOrder = 0
    object ButtonDraw: TButton
      Left = 8
      Top = 8
      Width = 75
      Height = 25
      Caption = 'Draw'
      TabOrder = 0
      OnClick = ButtonDrawClick
    end
    object EditName: TEdit
      Left = 96
      Top = 8
      Width = 121
      Height = 23
      TabOrder = 1
      Text = 'circle'
    end
  end
  object MemoLog: TMemo
    Left = 0
    Top = 41
    Width = 400
    Height = 259
    Align = alClient
    Lines.Strings = (
      'ready')
    TabOrder = 1
  end
end
