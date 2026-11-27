{ Classic ISO 7185 Pascal: nested routines, forward declarations,
  variant records, with statements, sets, conformant arrays, labels. }
program Bank(input, output, journal);

label 99;

const
  MaxAccounts = 10;
  Rate = 0.05;

type
  { Kinds of account }
  Kind = (checking, savings, loan);
  Index = 1..MaxAccounts;
  Name = packed array [1..20] of char;
  KindSet = set of Kind;
  AccountPtr = ^Account;
  Account = record
    id: Index;
    owner: Name;
    next: AccountPtr;
    case kind: Kind of
      checking: (overdraft: integer);
      savings: (interest: real);
      loan: (principal, term: integer)
  end;
  Ledger = file of Account;

var
  journal: text;
  accounts: array [Index] of Account;
  count: 0..MaxAccounts;
  head: AccountPtr;
  allowed: KindSet;

procedure Report(var a: Account); forward;

{ Sum of all balances, computed by deeply nested helpers }
function Total(limit: integer): real;
var
  sum: real;
  i: integer;

  procedure Add(x: real);

    procedure Check;
    begin
      if x < 0 then
        goto 99
    end;

  begin
    Check;
    sum := sum + x
  end;

begin
  sum := 0;
  for i := 1 to limit do
    with accounts[i] do
      case kind of
        checking: Add(overdraft);
        savings: Add(interest * Rate);
        loan: Add(-principal)
      end;
  Total := sum
end;

procedure Report;
begin
  with a do
  begin
    write(output, id: 3, ' ');
    if kind in allowed then
      writeln(owner)
  end
end;

procedure Apply(function f(x: real): real; var v: real);
begin
  v := f(v)
end;

function Twice(x: real): real;
begin
  Twice := 2 * x
end;

procedure Sum(var a: array [lo..hi: integer] of real; var s: real);
var
  k: integer;
begin
  s := 0;
  for k := lo to hi do
    s := s + a[k]
end;

begin
  count := 0;
  allowed := [checking, savings];
  new(head);
  head^.next := nil;
  head^.kind := savings;
  rewrite(journal);
  writeln(journal, Total(count): 10: 2);
  Report(accounts[1]);
99:
  writeln('done')
end.
