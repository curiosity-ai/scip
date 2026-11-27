//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utf8Bom/
  unit Utf8Bom;
//     ^^^^^^^ definition scip-delphi delphi fixture 1.0 Utf8Bom/
//             kind Module
//             display_name Utf8Bom
//             signature_documentation
//             > unit Utf8Bom
  
  // UTF-8 with BOM, as modern Delphi writes it: Ärger, 日本, 😀.
  
  interface
  
  uses Ansi;
//     ^^^^ reference scip-delphi delphi fixture 1.0 Ansi/
  
  const
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utf8Bom/`Grüße`.
    Grüße = 'Hallö 日本 😀';
//  ^^^^^ definition scip-delphi delphi fixture 1.0 Utf8Bom/`Grüße`.
//        kind Constant
//        display_name Grüße
//        signature_documentation
//        > const Grüße = 'Hallö 日本 😀'
//                       ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utf8Bom/`Grüße`.
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utf8Bom/Describe().
  function Describe: string;
//         ^^^^^^^^ definition scip-delphi delphi fixture 1.0 Utf8Bom/Describe().
//                  kind Function
//                  display_name Describe
//                  signature_documentation
//                  > function Describe: string
//                   ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                         ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utf8Bom/Describe().
  
  implementation
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Utf8Bom/Describe().
  function Describe: string;
//         ^^^^^^^^ reference scip-delphi delphi fixture 1.0 Utf8Bom/Describe().
//                   ^^^^^^ reference scip-delphi delphi rtl . System/string#
  const
//  ⌄ enclosing_range_start local 0
    Mehrzeilig = '''
//  ^^^^^^^^^^ definition local 0
//             kind Constant
//             display_name Mehrzeilig
//             signature_documentation
//             > const Mehrzeilig = ''' first line 😀 second line '''
      first line 😀
      second line
      ''';
//       ⌃ enclosing_range_end local 0
  begin
    Result := Grüße + Beschreibung(gGroß) + Mehrzeilig + #13#10 + #$263A;
//            ^^^^^ reference scip-delphi delphi fixture 1.0 Utf8Bom/`Grüße`.
//                    ^^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Ansi/Beschreibung().
//                                 ^^^^^ reference scip-delphi delphi fixture 1.0 Ansi/`TGröße`#`gGroß`.
//                                          ^^^^^^^^^^ reference local 0
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utf8Bom/Describe().
  
  end.
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Utf8Bom/
  
