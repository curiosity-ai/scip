// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Vcl.ExtCtrls;

interface

uses System.Classes, Vcl.Controls, Vcl.Graphics, Vcl.StdCtrls;

type
  TBevelStyle = (bsLowered, bsRaised);
  TShapeType = (stRectangle, stSquare, stRoundRect, stRoundSquare, stEllipse, stCircle);

  TTimer = class(TComponent)
  public
    constructor Create(AOwner: TComponent); override;
    property Enabled: Boolean read FEnabled write SetEnabled;
    property Interval: Cardinal read FInterval write SetInterval;
    property OnTimer: TNotifyEvent read FOnTimer write SetOnTimer;
  end;

  TCustomPanel = class(TCustomControl)
  public
    property Alignment: TAlignment read FAlignment write SetAlignment;
    property BevelInner: TBevelCut read FBevelInner write SetBevelInner;
    property BevelOuter: TBevelCut read FBevelOuter write SetBevelOuter;
    property BorderStyle: TBorderStyle read FBorderStyle write SetBorderStyle;
    property ParentBackground: Boolean read GetParentBackground write SetParentBackground;
  end;

  TPanel = class(TCustomPanel);

  TImage = class(TGraphicControl)
  public
    property Center: Boolean read FCenter write SetCenter;
    property Picture: TPicture read FPicture write SetPicture;
    property Proportional: Boolean read FProportional write SetProportional;
    property Stretch: Boolean read FStretch write SetStretch;
    property Transparent: Boolean read GetTransparent write SetTransparent;
  end;

  TShape = class(TGraphicControl)
  public
    property Brush: TBrush read FBrush write SetBrush;
    property Pen: TPen read FPen write SetPen;
    property Shape: TShapeType read FShape write SetShape;
  end;

  TBevel = class(TGraphicControl)
  public
    property Style: TBevelStyle read FStyle write SetStyle;
  end;

  TSplitter = class(TGraphicControl)
  public
    property MinSize: Integer read FMinSize write FMinSize;
  end;

  TPaintBox = class(TGraphicControl)
  public
    property OnPaint: TNotifyEvent read FOnPaint write FOnPaint;
  end;

  TCustomRadioGroup = class(TCustomGroupBox)
  public
    property ItemIndex: Integer read FItemIndex write SetItemIndex;
    property Items: TStrings read FItems write SetItems;
  end;

  TRadioGroup = class(TCustomRadioGroup);

  TCustomLinkLabel = class(TWinControl);

  TLinkLabel = class(TCustomLinkLabel);

implementation

end.
