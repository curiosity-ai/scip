  (* An ISO program written before Turbo Pascal: words that later became
     reserved (operator, string, object) are ordinary identifiers here. *)
  program Scanner(input, output);
//        ^^^^^^^ definition scip-pascal . iso . Scanner/
//                kind Module
//                display_name Scanner
//                signature_documentation
//                > program Scanner
//                documentation
//                > An ISO program written before Turbo Pascal: words that later became
//                > reserved (operator, string, object) are ordinary identifiers here.
//                ^^^^^ reference scip-pascal fpc rtl . System/Input.
//                       ^^^^^^ reference scip-pascal fpc rtl . System/Output.
  
  type
//  ⌄ enclosing_range_start scip-pascal . iso . Scanner/operator#
    operator = (plus, minus, times);
//  ^^^^^^^^ definition scip-pascal . iso . Scanner/operator#
//           kind Enum
//           display_name operator
//           signature_documentation
//           > type operator = (plus, minus, times)
//              ^^^^ definition scip-pascal . iso . Scanner/operator#plus.
//                   kind EnumMember
//                   display_name plus
//                   signature_documentation
//                   > operator.plus
//                    ^^^^^ definition scip-pascal . iso . Scanner/operator#minus.
//                          kind EnumMember
//                          display_name minus
//                          signature_documentation
//                          > operator.minus
//                           ^^^^^ definition scip-pascal . iso . Scanner/operator#times.
//                                 kind EnumMember
//                                 display_name times
//                                 signature_documentation
//                                 > operator.times
//                                 ⌃ enclosing_range_end scip-pascal . iso . Scanner/operator#
//  ⌄ enclosing_range_start scip-pascal . iso . Scanner/string#
    string = packed array [1..16] of char;
//  ^^^^^^ definition scip-pascal . iso . Scanner/string#
//         kind Type
//         display_name string
//         signature_documentation
//         > type string = packed array [1..16] of char
//                                   ^^^^ reference scip-pascal fpc rtl . System/Char#
//                                       ⌃ enclosing_range_end scip-pascal . iso . Scanner/string#
//  ⌄ enclosing_range_start scip-pascal . iso . Scanner/object#
    object = record
//  ^^^^^^ definition scip-pascal . iso . Scanner/object#
//         kind Struct
//         display_name object
//         signature_documentation
//         > type object = record
      op: operator;
//    ^^ definition scip-pascal . iso . Scanner/object#op.
//       kind Field
//       display_name op
//       signature_documentation
//       > op: operator
//        ^^^^^^^^ reference scip-pascal . iso . Scanner/operator#
      text: string
//    ^^^^ definition scip-pascal . iso . Scanner/object#text.
//         kind Field
//         display_name text
//         signature_documentation
//         > text: string
//          ^^^^^^ reference scip-pascal . iso . Scanner/string#
    end;
//     ⌃ enclosing_range_end scip-pascal . iso . Scanner/object#
  
  var
    tokens: array [1..8] of object;
//  ^^^^^^ definition scip-pascal . iso . Scanner/tokens.
//         kind Variable
//         display_name tokens
//         signature_documentation
//         > var tokens: array [1..8] of object
//                          ^^^^^^ reference scip-pascal . iso . Scanner/object#
    n: integer;
//  ^ definition scip-pascal . iso . Scanner/n.
//    kind Variable
//    display_name n
//    signature_documentation
//    > var n: integer
//     ^^^^^^^ reference scip-pascal fpc rtl . System/Integer#
  
//⌄ enclosing_range_start scip-pascal . iso . Scanner/Classify().
  function Classify(c: char): operator;
//         ^^^^^^^^ definition scip-pascal . iso . Scanner/Classify().
//                  kind Function
//                  display_name Classify
//                  signature_documentation
//                  > function Classify(c: char): operator
//                  ^ definition local 0
//                    kind Parameter
//                    display_name c
//                    signature_documentation
//                    > c: char
//                     ^^^^ reference scip-pascal fpc rtl . System/Char#
//                            ^^^^^^^^ reference scip-pascal . iso . Scanner/operator#
  begin
    if c = '+' then Classify := plus
//     ^ reference local 0
//                  ^^^^^^^^ reference scip-pascal . iso . Scanner/Classify().
//                              ^^^^ reference scip-pascal . iso . Scanner/operator#plus.
    else if c = '-' then Classify := minus
//          ^ reference local 0
//                       ^^^^^^^^ reference scip-pascal . iso . Scanner/Classify().
//                                   ^^^^^ reference scip-pascal . iso . Scanner/operator#minus.
    else Classify := times
//       ^^^^^^^^ reference scip-pascal . iso . Scanner/Classify().
//                   ^^^^^ reference scip-pascal . iso . Scanner/operator#times.
  end;
//  ⌃ enclosing_range_end scip-pascal . iso . Scanner/Classify().
  
  begin
    n := 1;
//  ^ reference scip-pascal . iso . Scanner/n.
    tokens[n].op := Classify('+');
//  ^^^^^^ reference scip-pascal . iso . Scanner/tokens.
//         ^ reference scip-pascal . iso . Scanner/n.
//            ^^ reference scip-pascal . iso . Scanner/object#op.
//                  ^^^^^^^^ reference scip-pascal . iso . Scanner/Classify().
    with tokens[n] do
//       ^^^^^^ reference scip-pascal . iso . Scanner/tokens.
//              ^ reference scip-pascal . iso . Scanner/n.
      if op = plus then
//       ^^ reference scip-pascal . iso . Scanner/object#op.
//            ^^^^ reference scip-pascal . iso . Scanner/operator#plus.
        writeln('plus')
//      ^^^^^^^ reference scip-pascal fpc rtl . System/WriteLn().
  end.
  
