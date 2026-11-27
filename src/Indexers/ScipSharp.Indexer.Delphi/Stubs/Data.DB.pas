// Declaration-only stub written for scip-delphi (not Embarcadero source).
unit Data.DB;

interface

uses System.Classes, System.SysUtils;

type
  TFieldType = (ftUnknown, ftString, ftSmallint, ftInteger, ftWord, ftBoolean, ftFloat, ftCurrency, ftBCD, ftDate,
    ftTime, ftDateTime, ftBytes, ftVarBytes, ftAutoInc, ftBlob, ftMemo, ftGraphic, ftWideString, ftLargeint,
    ftGuid, ftWideMemo, ftLongWord, ftShortint, ftByte, ftExtended, ftSingle);
  TDataSetState = (dsInactive, dsBrowse, dsEdit, dsInsert, dsSetKey, dsCalcFields, dsFilter, dsNewValue, dsOldValue);
  TDataSetNotifyEvent = procedure(DataSet: TDataSet) of object;
  TParamType = (ptUnknown, ptInput, ptOutput, ptInputOutput, ptResult);

  EDatabaseError = class(Exception);

  TField = class(TComponent)
  public
    procedure Clear; virtual;
    property AsBoolean: Boolean read GetAsBoolean write SetAsBoolean;
    property AsCurrency: Currency read GetAsCurrency write SetAsCurrency;
    property AsDateTime: TDateTime read GetAsDateTime write SetAsDateTime;
    property AsFloat: Double read GetAsFloat write SetAsFloat;
    property AsInteger: Integer read GetAsInteger write SetAsInteger;
    property AsLargeInt: Int64 read GetAsLargeInt write SetAsLargeInt;
    property AsString: string read GetAsString write SetAsString;
    property AsVariant: Variant read GetAsVariant write SetAsVariant;
    property DataType: TFieldType read FDataType;
    property DisplayLabel: string read GetDisplayLabel write SetDisplayLabel;
    property DisplayText: string read GetDisplayText;
    property FieldName: string read FFieldName write SetFieldName;
    property IsNull: Boolean read GetIsNull;
    property ReadOnly: Boolean read FReadOnly write SetReadOnly;
    property Required: Boolean read FRequired write FRequired;
    property Size: Integer read FSize write SetSize;
    property Value: Variant read GetAsVariant write SetAsVariant;
    property Visible: Boolean read FVisible write SetVisible;
  end;

  TStringField = class(TField);
  TIntegerField = class(TField);
  TFloatField = class(TField);
  TDateTimeField = class(TField);
  TBooleanField = class(TField);
  TBlobField = class(TField);
  TMemoField = class(TBlobField);

  TFields = class(TObject)
  public
    function FindField(const FieldName: string): TField;
    function FieldByName(const FieldName: string): TField;
    property Count: Integer read GetCount;
    property Fields[Index: Integer]: TField read GetField write SetField; default;
  end;

  TParam = class(TCollectionItem)
  public
    property AsInteger: Integer read GetAsInteger write SetAsInteger;
    property AsString: string read GetAsString write SetAsString;
    property AsDateTime: TDateTime read GetAsDateTime write SetAsDateTime;
    property DataType: TFieldType read FDataType write SetDataType;
    property Name: string read FName write FName;
    property Value: Variant read GetAsVariant write SetAsVariant;
  end;

  TParams = class(TCollection)
  public
    function ParamByName(const Value: string): TParam;
    function FindParam(const Value: string): TParam;
    property Items[Index: Integer]: TParam read GetItem write SetItem; default;
  end;

  TDataSet = class(TComponent)
  public
    procedure Append;
    procedure Cancel; virtual;
    procedure Close;
    procedure Delete;
    procedure DisableControls;
    procedure Edit;
    procedure EnableControls;
    function FieldByName(const FieldName: string): TField;
    function FindField(const FieldName: string): TField;
    procedure First;
    procedure Insert;
    function IsEmpty: Boolean;
    procedure Last;
    function Locate(const KeyFields: string; const KeyValues: Variant; Options: Integer): Boolean; virtual;
    procedure Next;
    procedure Open;
    procedure Post; virtual;
    procedure Prior;
    procedure Refresh;
    property Active: Boolean read GetActive write SetActive;
    property Bof: Boolean read FBOF;
    property Eof: Boolean read FEOF;
    property FieldCount: Integer read GetFieldCount;
    property Fields: TFields read FFields;
    property FieldValues[const FieldName: string]: Variant read GetFieldValue write SetFieldValue; default;
    property RecNo: Integer read GetRecNo write SetRecNo;
    property RecordCount: Integer read GetRecordCount;
    property State: TDataSetState read FState;
    property AfterOpen: TDataSetNotifyEvent read FAfterOpen write FAfterOpen;
    property AfterPost: TDataSetNotifyEvent read FAfterPost write FAfterPost;
    property AfterScroll: TDataSetNotifyEvent read FAfterScroll write FAfterScroll;
    property BeforePost: TDataSetNotifyEvent read FBeforePost write FBeforePost;
    property OnNewRecord: TDataSetNotifyEvent read FOnNewRecord write FOnNewRecord;
  end;

  TDataSource = class(TComponent)
  public
    property DataSet: TDataSet read FDataSet write SetDataSet;
    property Enabled: Boolean read FEnabled write SetEnabled;
    property State: TDataSetState read FState;
  end;

implementation

end.
