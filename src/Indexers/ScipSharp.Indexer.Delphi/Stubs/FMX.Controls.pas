// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit FMX.Controls;

interface

uses System.Classes, System.Types, System.UITypes, FMX.Types;

type
  TControl = class(TFmxObject)
  public
    procedure Repaint;
    procedure SetFocus;
    procedure BringToFront;
    procedure SendToBack;
    property Align: TAlignLayout read FAlign write SetAlign;
    property Enabled: Boolean read FEnabled write SetEnabled;
    property Height: Single read GetHeight write SetHeight;
    property Hint: string read FHint write SetHint;
    property HitTest: Boolean read FHitTest write SetHitTest;
    property Opacity: Single read FOpacity write SetOpacity;
    property Position: TPointF read GetPosition write SetPosition;
    property Visible: Boolean read FVisible write SetVisible;
    property Width: Single read GetWidth write SetWidth;
    property OnClick: TNotifyEvent read FOnClick write SetOnClick;
    property OnDblClick: TNotifyEvent read FOnDblClick write FOnDblClick;
    property OnResize: TNotifyEvent read FOnResize write FOnResize;
  end;

  TStyledControl = class(TControl)
  public
    property StyleLookup: string read FStyleLookup write SetStyleLookup;
  end;

  TTextControl = class(TStyledControl)
  public
    property Text: string read GetText write SetText;
  end;

  TPresentedControl = class(TStyledControl);

implementation

end.
