// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit FMX.StdCtrls;

interface

uses System.Classes, System.UITypes, FMX.Types, FMX.Controls;

type
  TPanel = class(TPresentedControl);

  TLabel = class(TTextControl)
  public
    property AutoSize: Boolean read FAutoSize write SetAutoSize;
    property WordWrap: Boolean read GetWordWrap write SetWordWrap;
  end;

  TCustomButton = class(TTextControl)
  public
    property ModalResult: TModalResult read FModalResult write FModalResult;
  end;

  TButton = class(TCustomButton);

  TSpeedButton = class(TCustomButton);

  TCheckBox = class(TTextControl)
  public
    property IsChecked: Boolean read GetIsChecked write SetIsChecked;
    property OnChange: TNotifyEvent read FOnChange write FOnChange;
  end;

  TRadioButton = class(TTextControl)
  public
    property GroupName: string read GetGroupName write SetGroupName;
    property IsChecked: Boolean read GetIsChecked write SetIsChecked;
  end;

  TProgressBar = class(TPresentedControl)
  public
    property Max: Single read GetMax write SetMax;
    property Min: Single read GetMin write SetMin;
    property Value: Single read GetValue write SetValue;
  end;

  TTrackBar = class(TPresentedControl)
  public
    property Value: Single read GetValue write SetValue;
    property OnChange: TNotifyEvent read FOnChange write FOnChange;
  end;

implementation

end.
