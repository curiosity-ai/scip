unit Utf8Bom;

// UTF-8 with BOM, as modern Delphi writes it: Ärger, 日本, 😀.

interface

uses Ansi;

const
  Grüße = 'Hallö 日本 😀';

function Describe: string;

implementation

function Describe: string;
const
  Mehrzeilig = '''
    first line 😀
    second line
    ''';
begin
  Result := Grüße + Beschreibung(gGroß) + Mehrzeilig + #13#10 + #$263A;
end;

end.
