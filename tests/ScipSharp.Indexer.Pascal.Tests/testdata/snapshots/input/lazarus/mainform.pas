unit MainForm;

{$mode objfpc}{$H+}

interface

uses
  Classes, SysUtils, Forms, Controls, StdCtrls;

type
  { TForm1 }
  TForm1 = class(TForm)
    Button1: TButton;
    Memo1: TMemo;
    procedure Button1Click(Sender: TObject);
    procedure FormCreate(Sender: TObject);
  private
    FClicks: Integer;
  end;

var
  Form1: TForm1;

implementation

{$R *.lfm}

{ TForm1 }

procedure TForm1.FormCreate(Sender: TObject);
begin
  FClicks := 0;
  Caption := 'Demo';
end;

procedure TForm1.Button1Click(Sender: TObject);
begin
  Inc(FClicks);
  Memo1.Lines.Add(Format('Clicked %d times', [FClicks]));
  Button1.Caption := IntToStr(FClicks);
end;

end.
