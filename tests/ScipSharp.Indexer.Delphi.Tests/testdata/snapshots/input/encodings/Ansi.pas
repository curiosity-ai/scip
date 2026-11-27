unit Ansi;

{ Saved as Windows-1252 ("ANSI"), as Delphi 7 does: café, naïve, “quotes”. }

interface

type
  TGröße = (gKlein, gGroß);

function Beschreibung(G: TGröße): string;

implementation

function Beschreibung(G: TGröße): string;
begin
  case G of
    gKlein: Result := 'kléin';
    gGroß: Result := 'groß €';
  end;
end;

end.
