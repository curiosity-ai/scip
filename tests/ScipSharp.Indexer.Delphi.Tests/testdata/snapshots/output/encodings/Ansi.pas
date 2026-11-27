//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Ansi/
  unit Ansi;
//     ^^^^ definition scip-delphi delphi fixture 1.0 Ansi/
//          kind Module
//          display_name Ansi
//          signature_documentation
//          > unit Ansi
  
  { Saved as Windows-1252 ("ANSI"), as Delphi 7 does: café, naïve, “quotes”. }
  
  interface
  
  type
//  ⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Ansi/`TGröße`#
    TGröße = (gKlein, gGroß);
//  ^^^^^^ definition scip-delphi delphi fixture 1.0 Ansi/`TGröße`#
//         kind Enum
//         display_name TGröße
//         signature_documentation
//         > type TGröße = (gKlein, gGroß)
//            ^^^^^^ definition scip-delphi delphi fixture 1.0 Ansi/`TGröße`#gKlein.
//                   kind EnumMember
//                   display_name gKlein
//                   signature_documentation
//                   > TGröße.gKlein
//                    ^^^^^ definition scip-delphi delphi fixture 1.0 Ansi/`TGröße`#`gGroß`.
//                          kind EnumMember
//                          display_name gGroß
//                          signature_documentation
//                          > TGröße.gGroß
//                          ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Ansi/`TGröße`#
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Ansi/Beschreibung().
  function Beschreibung(G: TGröße): string;
//         ^^^^^^^^^^^^ definition scip-delphi delphi fixture 1.0 Ansi/Beschreibung().
//                      kind Function
//                      display_name Beschreibung
//                      signature_documentation
//                      > function Beschreibung(G: TGröße): string
//                      ^ definition scip-delphi delphi fixture 1.0 Ansi/Beschreibung().(G)
//                        kind Parameter
//                        display_name G
//                        signature_documentation
//                        > G: TGröße
//                         ^^^^^^ reference scip-delphi delphi fixture 1.0 Ansi/`TGröße`#
//                                  ^^^^^^ reference scip-delphi delphi rtl . System/string#
//                                        ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Ansi/Beschreibung().
  
  implementation
  
//⌄ enclosing_range_start scip-delphi delphi fixture 1.0 Ansi/Beschreibung().
  function Beschreibung(G: TGröße): string;
//         ^^^^^^^^^^^^ reference scip-delphi delphi fixture 1.0 Ansi/Beschreibung().
//                      ^ reference scip-delphi delphi fixture 1.0 Ansi/Beschreibung().(G)
//                         ^^^^^^ reference scip-delphi delphi fixture 1.0 Ansi/`TGröße`#
//                                  ^^^^^^ reference scip-delphi delphi rtl . System/string#
  begin
    case G of
//       ^ reference scip-delphi delphi fixture 1.0 Ansi/Beschreibung().(G)
      gKlein: Result := 'kléin';
//    ^^^^^^ reference scip-delphi delphi fixture 1.0 Ansi/`TGröße`#gKlein.
      gGroß: Result := 'groß €';
//    ^^^^^ reference scip-delphi delphi fixture 1.0 Ansi/`TGröße`#`gGroß`.
    end;
  end;
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Ansi/Beschreibung().
  
  end.
//   ⌃ enclosing_range_end scip-delphi delphi fixture 1.0 Ansi/
  
