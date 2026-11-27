  { Classic ISO 7185 Pascal: nested routines, forward declarations,
    variant records, with statements, sets, conformant arrays, labels. }
  program Bank(input, output, journal);
//        ^^^^ definition scip-pascal . iso . Bank/
//             kind Module
//             display_name Bank
//             signature_documentation
//             > program Bank
//             documentation
//             > Classic ISO 7185 Pascal: nested routines, forward declarations,
//             > variant records, with statements, sets, conformant arrays, labels.
//             ^^^^^ reference scip-pascal fpc rtl . System/Input.
//                    ^^^^^^ reference scip-pascal fpc rtl . System/Output.
//                            ^^^^^^^ reference scip-pascal . iso . Bank/journal.
  
  label 99;
//      ^^ definition local 0
//         display_name 99
//         signature_documentation
//         > label 99
  
  const
//  ⌄ enclosing_range_start scip-pascal . iso . Bank/MaxAccounts.
    MaxAccounts = 10;
//  ^^^^^^^^^^^ definition scip-pascal . iso . Bank/MaxAccounts.
//              kind Constant
//              display_name MaxAccounts
//              signature_documentation
//              > const MaxAccounts = 10
//                 ⌃ enclosing_range_end scip-pascal . iso . Bank/MaxAccounts.
//  ⌄ enclosing_range_start scip-pascal . iso . Bank/Rate.
    Rate = 0.05;
//  ^^^^ definition scip-pascal . iso . Bank/Rate.
//       kind Constant
//       display_name Rate
//       signature_documentation
//       > const Rate = 0.05
//            ⌃ enclosing_range_end scip-pascal . iso . Bank/Rate.
  
  type
    { Kinds of account }
//  ⌄ enclosing_range_start scip-pascal . iso . Bank/Kind#
    Kind = (checking, savings, loan);
//  ^^^^ definition scip-pascal . iso . Bank/Kind#
//       kind Enum
//       display_name Kind
//       signature_documentation
//       > type Kind = (checking, savings, loan)
//       documentation
//       > Kinds of account
//          ^^^^^^^^ definition scip-pascal . iso . Bank/Kind#checking.
//                   kind EnumMember
//                   display_name checking
//                   signature_documentation
//                   > Kind.checking
//                    ^^^^^^^ definition scip-pascal . iso . Bank/Kind#savings.
//                            kind EnumMember
//                            display_name savings
//                            signature_documentation
//                            > Kind.savings
//                             ^^^^ definition scip-pascal . iso . Bank/Kind#loan.
//                                  kind EnumMember
//                                  display_name loan
//                                  signature_documentation
//                                  > Kind.loan
//                                  ⌃ enclosing_range_end scip-pascal . iso . Bank/Kind#
//  ⌄ enclosing_range_start scip-pascal . iso . Bank/Index#
    Index = 1..MaxAccounts;
//  ^^^^^ definition scip-pascal . iso . Bank/Index#
//        kind Type
//        display_name Index
//        signature_documentation
//        > type Index = 1..MaxAccounts
//             ^^^^^^^^^^^ reference scip-pascal . iso . Bank/MaxAccounts.
//                        ⌃ enclosing_range_end scip-pascal . iso . Bank/Index#
//  ⌄ enclosing_range_start scip-pascal . iso . Bank/Name#
    Name = packed array [1..20] of char;
//  ^^^^ definition scip-pascal . iso . Bank/Name#
//       kind Type
//       display_name Name
//       signature_documentation
//       > type Name = packed array [1..20] of char
//                                 ^^^^ reference scip-pascal fpc rtl . System/Char#
//                                     ⌃ enclosing_range_end scip-pascal . iso . Bank/Name#
//  ⌄ enclosing_range_start scip-pascal . iso . Bank/KindSet#
    KindSet = set of Kind;
//  ^^^^^^^ definition scip-pascal . iso . Bank/KindSet#
//          kind Type
//          display_name KindSet
//          signature_documentation
//          > type KindSet = set of Kind
//                   ^^^^ reference scip-pascal . iso . Bank/Kind#
//                       ⌃ enclosing_range_end scip-pascal . iso . Bank/KindSet#
//  ⌄ enclosing_range_start scip-pascal . iso . Bank/AccountPtr#
    AccountPtr = ^Account;
//  ^^^^^^^^^^ definition scip-pascal . iso . Bank/AccountPtr#
//             kind Type
//             display_name AccountPtr
//             signature_documentation
//             > type AccountPtr = ^Account
//                ^^^^^^^ reference scip-pascal . iso . Bank/Account#
//                       ⌃ enclosing_range_end scip-pascal . iso . Bank/AccountPtr#
//  ⌄ enclosing_range_start scip-pascal . iso . Bank/Account#
    Account = record
//  ^^^^^^^ definition scip-pascal . iso . Bank/Account#
//          kind Struct
//          display_name Account
//          signature_documentation
//          > type Account = record
      id: Index;
//    ^^ definition scip-pascal . iso . Bank/Account#id.
//       kind Field
//       display_name id
//       signature_documentation
//       > id: Index
//        ^^^^^ reference scip-pascal . iso . Bank/Index#
      owner: Name;
//    ^^^^^ definition scip-pascal . iso . Bank/Account#owner.
//          kind Field
//          display_name owner
//          signature_documentation
//          > owner: Name
//           ^^^^ reference scip-pascal . iso . Bank/Name#
      next: AccountPtr;
//    ^^^^ definition scip-pascal . iso . Bank/Account#next.
//         kind Field
//         display_name next
//         signature_documentation
//         > next: AccountPtr
//          ^^^^^^^^^^ reference scip-pascal . iso . Bank/AccountPtr#
      case kind: Kind of
//         ^^^^ definition scip-pascal . iso . Bank/Account#kind.
//              kind Field
//              display_name kind
//              signature_documentation
//              > case kind: Kind
//               ^^^^ reference scip-pascal . iso . Bank/Kind#
        checking: (overdraft: integer);
//      ^^^^^^^^ reference scip-pascal . iso . Bank/Kind#checking.
//                 ^^^^^^^^^ definition scip-pascal . iso . Bank/Account#overdraft.
//                           kind Field
//                           display_name overdraft
//                           signature_documentation
//                           > overdraft: integer
//                            ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
        savings: (interest: real);
//      ^^^^^^^ reference scip-pascal . iso . Bank/Kind#savings.
//                ^^^^^^^^ definition scip-pascal . iso . Bank/Account#interest.
//                         kind Field
//                         display_name interest
//                         signature_documentation
//                         > interest: real
//                          ^^^^ reference scip-pascal fpc rtl . System/Real#
        loan: (principal, term: integer)
//      ^^^^ reference scip-pascal . iso . Bank/Kind#loan.
//             ^^^^^^^^^ definition scip-pascal . iso . Bank/Account#principal.
//                       kind Field
//                       display_name principal
//                       signature_documentation
//                       > principal: integer
//                        ^^^^ definition scip-pascal . iso . Bank/Account#term.
//                             kind Field
//                             display_name term
//                             signature_documentation
//                             > term: integer
//                              ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
    end;
//     ⌃ enclosing_range_end scip-pascal . iso . Bank/Account#
//  ⌄ enclosing_range_start scip-pascal . iso . Bank/Ledger#
    Ledger = file of Account;
//  ^^^^^^ definition scip-pascal . iso . Bank/Ledger#
//         kind Type
//         display_name Ledger
//         signature_documentation
//         > type Ledger = file of Account
//                   ^^^^^^^ reference scip-pascal . iso . Bank/Account#
//                          ⌃ enclosing_range_end scip-pascal . iso . Bank/Ledger#
  
  var
    journal: text;
//  ^^^^^^^ definition scip-pascal . iso . Bank/journal.
//          kind Variable
//          display_name journal
//          signature_documentation
//          > var journal: text
//           ^^^^ reference scip-pascal fpc rtl . System/Text#
    accounts: array [Index] of Account;
//  ^^^^^^^^ definition scip-pascal . iso . Bank/accounts.
//           kind Variable
//           display_name accounts
//           signature_documentation
//           > var accounts: array [Index] of Account
//                   ^^^^^ reference scip-pascal . iso . Bank/Index#
//                             ^^^^^^^ reference scip-pascal . iso . Bank/Account#
    count: 0..MaxAccounts;
//  ^^^^^ definition scip-pascal . iso . Bank/count.
//        kind Variable
//        display_name count
//        signature_documentation
//        > var count: 0..MaxAccounts
//            ^^^^^^^^^^^ reference scip-pascal . iso . Bank/MaxAccounts.
    head: AccountPtr;
//  ^^^^ definition scip-pascal . iso . Bank/head.
//       kind Variable
//       display_name head
//       signature_documentation
//       > var head: AccountPtr
//        ^^^^^^^^^^ reference scip-pascal . iso . Bank/AccountPtr#
    allowed: KindSet;
//  ^^^^^^^ definition scip-pascal . iso . Bank/allowed.
//          kind Variable
//          display_name allowed
//          signature_documentation
//          > var allowed: KindSet
//           ^^^^^^^ reference scip-pascal . iso . Bank/KindSet#
  
  procedure Report(var a: Account); forward;
//          ^^^^^^ forward_definition scip-pascal . iso . Bank/Report().
//                     ^ definition local 1
//                       kind Parameter
//                       display_name a
//                       signature_documentation
//                       > var a: Account
//                        ^^^^^^^ reference scip-pascal . iso . Bank/Account#
  
  { Sum of all balances, computed by deeply nested helpers }
//⌄ enclosing_range_start scip-pascal . iso . Bank/Total().
  function Total(limit: integer): real;
//         ^^^^^ definition scip-pascal . iso . Bank/Total().
//               kind Function
//               display_name Total
//               signature_documentation
//               > function Total(limit: integer): real
//               documentation
//               > Sum of all balances, computed by deeply nested helpers
//               ^^^^^ definition local 2
//                     kind Parameter
//                     display_name limit
//                     signature_documentation
//                     > limit: integer
//                      ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
//                                ^^^^ reference scip-pascal fpc rtl . System/Real#
  var
    sum: real;
//  ^^^ definition local 3
//      kind Variable
//      display_name sum
//      signature_documentation
//      > var sum: real
//       ^^^^ reference scip-pascal fpc rtl . System/Real#
    i: integer;
//  ^ definition local 4
//    kind Variable
//    display_name i
//    signature_documentation
//    > var i: integer
//     ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  
//  ⌄ enclosing_range_start scip-pascal . iso . Bank/Total().Add().
    procedure Add(x: real);
//            ^^^ definition scip-pascal . iso . Bank/Total().Add().
//                kind Function
//                display_name Add
//                signature_documentation
//                > procedure Add(x: real)
//                ^ definition local 5
//                  kind Parameter
//                  display_name x
//                  signature_documentation
//                  > x: real
//                   ^^^^ reference scip-pascal fpc rtl . System/Real#
  
//    ⌄ enclosing_range_start scip-pascal . iso . Bank/Total().Add().Check().
      procedure Check;
//              ^^^^^ definition scip-pascal . iso . Bank/Total().Add().Check().
//                    kind Function
//                    display_name Check
//                    signature_documentation
//                    > procedure Check
      begin
        if x < 0 then
//         ^ reference local 5
          goto 99
//             ^^ reference local 0
      end;
//      ⌃ enclosing_range_end scip-pascal . iso . Bank/Total().Add().Check().
  
    begin
      Check;
//    ^^^^^ reference scip-pascal . iso . Bank/Total().Add().Check().
      sum := sum + x
//    ^^^ reference local 3
//           ^^^ reference local 3
//                 ^ reference local 5
    end;
//    ⌃ enclosing_range_end scip-pascal . iso . Bank/Total().Add().
  
  begin
    sum := 0;
//  ^^^ reference local 3
    for i := 1 to limit do
//      ^ reference local 4
//                ^^^^^ reference local 2
      with accounts[i] do
//         ^^^^^^^^ reference scip-pascal . iso . Bank/accounts.
//                  ^ reference local 4
        case kind of
//           ^^^^ reference scip-pascal . iso . Bank/Account#kind.
          checking: Add(overdraft);
//        ^^^^^^^^ reference scip-pascal . iso . Bank/Kind#checking.
//                  ^^^ reference scip-pascal . iso . Bank/Total().Add().
//                      ^^^^^^^^^ reference scip-pascal . iso . Bank/Account#overdraft.
          savings: Add(interest * Rate);
//        ^^^^^^^ reference scip-pascal . iso . Bank/Kind#savings.
//                 ^^^ reference scip-pascal . iso . Bank/Total().Add().
//                     ^^^^^^^^ reference scip-pascal . iso . Bank/Account#interest.
//                                ^^^^ reference scip-pascal . iso . Bank/Rate.
          loan: Add(-principal)
//        ^^^^ reference scip-pascal . iso . Bank/Kind#loan.
//              ^^^ reference scip-pascal . iso . Bank/Total().Add().
//                   ^^^^^^^^^ reference scip-pascal . iso . Bank/Account#principal.
        end;
    Total := sum
//  ^^^^^ reference scip-pascal . iso . Bank/Total().
//           ^^^ reference local 3
  end;
//  ⌃ enclosing_range_end scip-pascal . iso . Bank/Total().
  
//⌄ enclosing_range_start scip-pascal . iso . Bank/Report().
  procedure Report;
//          ^^^^^^ definition scip-pascal . iso . Bank/Report().
//                 kind Function
//                 display_name Report
//                 signature_documentation
//                 > procedure Report(var a: Account)
  begin
    with a do
//       ^ reference local 1
    begin
      write(output, id: 3, ' ');
//    ^^^^^ reference scip-pascal fpc rtl . System/Write().
//          ^^^^^^ reference scip-pascal fpc rtl . System/Output.
//                  ^^ reference scip-pascal . iso . Bank/Account#id.
      if kind in allowed then
//       ^^^^ reference scip-pascal . iso . Bank/Account#kind.
//               ^^^^^^^ reference scip-pascal . iso . Bank/allowed.
        writeln(owner)
//      ^^^^^^^ reference scip-pascal fpc rtl . System/WriteLn().
//              ^^^^^ reference scip-pascal . iso . Bank/Account#owner.
    end
  end;
//  ⌃ enclosing_range_end scip-pascal . iso . Bank/Report().
  
//⌄ enclosing_range_start scip-pascal . iso . Bank/Apply().
  procedure Apply(function f(x: real): real; var v: real);
//          ^^^^^ definition scip-pascal . iso . Bank/Apply().
//                kind Function
//                display_name Apply
//                signature_documentation
//                > procedure Apply(function f(x: real): real; var v: real)
//                         ^ definition local 7
//                           kind Parameter
//                           display_name f
//                           signature_documentation
//                           > function f
//                           ^ definition local 6
//                             kind Parameter
//                             display_name x
//                             signature_documentation
//                             > x: real
//                              ^^^^ reference scip-pascal fpc rtl . System/Real#
//                                     ^^^^ reference scip-pascal fpc rtl . System/Real#
//                                               ^ definition local 8
//                                                 kind Parameter
//                                                 display_name v
//                                                 signature_documentation
//                                                 > var v: real
//                                                  ^^^^ reference scip-pascal fpc rtl . System/Real#
  begin
    v := f(v)
//  ^ reference local 8
//       ^ reference local 7
//         ^ reference local 8
  end;
//  ⌃ enclosing_range_end scip-pascal . iso . Bank/Apply().
  
//⌄ enclosing_range_start scip-pascal . iso . Bank/Twice().
  function Twice(x: real): real;
//         ^^^^^ definition scip-pascal . iso . Bank/Twice().
//               kind Function
//               display_name Twice
//               signature_documentation
//               > function Twice(x: real): real
//               ^ definition local 9
//                 kind Parameter
//                 display_name x
//                 signature_documentation
//                 > x: real
//                  ^^^^ reference scip-pascal fpc rtl . System/Real#
//                         ^^^^ reference scip-pascal fpc rtl . System/Real#
  begin
    Twice := 2 * x
//  ^^^^^ reference scip-pascal . iso . Bank/Twice().
//               ^ reference local 9
  end;
//  ⌃ enclosing_range_end scip-pascal . iso . Bank/Twice().
  
//⌄ enclosing_range_start scip-pascal . iso . Bank/Sum().
  procedure Sum(var a: array [lo..hi: integer] of real; var s: real);
//          ^^^ definition scip-pascal . iso . Bank/Sum().
//              kind Function
//              display_name Sum
//              signature_documentation
//              > procedure Sum(var a: array [lo..hi: integer] of real; var s: real)
//                  ^ definition local 10
//                    kind Parameter
//                    display_name a
//                    signature_documentation
//                    > var a: array [lo..hi: integer] of real
//                            ^^ definition local 11
//                               kind Parameter
//                               display_name lo
//                               signature_documentation
//                               > lo: integer
//                                ^^ definition local 12
//                                   kind Parameter
//                                   display_name hi
//                                   signature_documentation
//                                   > hi: integer
//                                    ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
//                                                ^^^^ reference scip-pascal fpc rtl . System/Real#
//                                                          ^ definition local 13
//                                                            kind Parameter
//                                                            display_name s
//                                                            signature_documentation
//                                                            > var s: real
//                                                             ^^^^ reference scip-pascal fpc rtl . System/Real#
  var
    k: integer;
//  ^ definition local 14
//    kind Variable
//    display_name k
//    signature_documentation
//    > var k: integer
//     ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  begin
    s := 0;
//  ^ reference local 13
    for k := lo to hi do
//      ^ reference local 14
//           ^^ reference local 11
//                 ^^ reference local 12
      s := s + a[k]
//    ^ reference local 13
//         ^ reference local 13
//             ^ reference local 10
//               ^ reference local 14
  end;
//  ⌃ enclosing_range_end scip-pascal . iso . Bank/Sum().
  
  begin
    count := 0;
//  ^^^^^ reference scip-pascal . iso . Bank/count.
    allowed := [checking, savings];
//  ^^^^^^^ reference scip-pascal . iso . Bank/allowed.
//              ^^^^^^^^ reference scip-pascal . iso . Bank/Kind#checking.
//                        ^^^^^^^ reference scip-pascal . iso . Bank/Kind#savings.
    new(head);
//  ^^^ reference scip-pascal fpc rtl . System/New().
//      ^^^^ reference scip-pascal . iso . Bank/head.
    head^.next := nil;
//  ^^^^ reference scip-pascal . iso . Bank/head.
//        ^^^^ reference scip-pascal . iso . Bank/Account#next.
    head^.kind := savings;
//  ^^^^ reference scip-pascal . iso . Bank/head.
//        ^^^^ reference scip-pascal . iso . Bank/Account#kind.
//                ^^^^^^^ reference scip-pascal . iso . Bank/Kind#savings.
    rewrite(journal);
//  ^^^^^^^ reference scip-pascal fpc rtl . System/Rewrite().
//          ^^^^^^^ reference scip-pascal . iso . Bank/journal.
    writeln(journal, Total(count): 10: 2);
//  ^^^^^^^ reference scip-pascal fpc rtl . System/WriteLn().
//          ^^^^^^^ reference scip-pascal . iso . Bank/journal.
//                   ^^^^^ reference scip-pascal . iso . Bank/Total().
//                         ^^^^^ reference scip-pascal . iso . Bank/count.
    Report(accounts[1]);
//  ^^^^^^ reference scip-pascal . iso . Bank/Report().
//         ^^^^^^^^ reference scip-pascal . iso . Bank/accounts.
  99:
//^^ reference local 0
    writeln('done')
//  ^^^^^^^ reference scip-pascal fpc rtl . System/WriteLn().
  end.
  
