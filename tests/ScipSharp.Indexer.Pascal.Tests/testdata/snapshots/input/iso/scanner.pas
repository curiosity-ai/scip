(* An ISO program written before Turbo Pascal: words that later became
   reserved (operator, string, object) are ordinary identifiers here. *)
program Scanner(input, output);

type
  operator = (plus, minus, times);
  string = packed array [1..16] of char;
  object = record
    op: operator;
    text: string
  end;

var
  tokens: array [1..8] of object;
  n: integer;

function Classify(c: char): operator;
begin
  if c = '+' then Classify := plus
  else if c = '-' then Classify := minus
  else Classify := times
end;

begin
  n := 1;
  tokens[n].op := Classify('+');
  with tokens[n] do
    if op = plus then
      writeln('plus')
end.
