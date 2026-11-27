*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYCALC/
         IDENTIFICATION DIVISION.
         PROGRAM-ID. PAYCALC.
*>                   ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYCALC/
*>                           kind Module
*>                           display_name PAYCALC
*>                           documentation
*>                           > ```cobol
*>                           > PROGRAM-ID. PAYCALC
*>                           > ```
         DATA DIVISION.
         LINKAGE SECTION.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYCALC/LK-TOTALS.
         01  LK-TOTALS.
*>           ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYCALC/LK-TOTALS.
*>                     kind Struct
*>                     display_name LK-TOTALS
*>                     documentation
*>                     > ```cobol
*>                     > 01 LK-TOTALS
*>                     > ```
*>                    ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYCALC/LK-TOTALS.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYCALC/LK-TOTALS.LK-TOTAL.
             05 LK-TOTAL             PIC S9(9)V99.
*>              ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYCALC/LK-TOTALS.LK-TOTAL.
*>                       kind Field
*>                       display_name LK-TOTAL
*>                       documentation
*>                       > ```cobol
*>                       > 05 LK-TOTAL PIC S9(9)V99
*>                       > ```
*>                                               ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYCALC/LK-TOTALS.LK-TOTAL.
             05 FILLER               PIC X(41).
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYCALC/LK-COUNT.
         01  LK-COUNT                PIC 9(5).
*>           ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYCALC/LK-COUNT.
*>                    kind Variable
*>                    display_name LK-COUNT
*>                    documentation
*>                    > ```cobol
*>                    > 01 LK-COUNT PIC 9(5)
*>                    > ```
*>                                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYCALC/LK-COUNT.
         PROCEDURE DIVISION USING LK-TOTALS LK-COUNT.
*>                                ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYCALC/LK-TOTALS.
*>                                          ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYCALC/LK-COUNT.
             IF LK-COUNT > 0
*>              ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYCALC/LK-COUNT.
                DIVIDE LK-COUNT INTO LK-TOTAL
*>                     ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYCALC/LK-COUNT.
*>                                   ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYCALC/LK-TOTALS.LK-TOTAL.
             END-IF
             GOBACK.
*>                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYCALC/
  
