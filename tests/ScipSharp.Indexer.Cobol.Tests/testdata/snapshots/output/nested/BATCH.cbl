        * Two separately compiled programs in one file (no END PROGRAM),
        * plus an alternate ENTRY point.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 FIRSTPGM/
         IDENTIFICATION DIVISION.
         PROGRAM-ID. FIRSTPGM.
*>                   ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 FIRSTPGM/
*>                            kind Module
*>                            display_name FIRSTPGM
*>                            documentation
*>                            > ```cobol
*>                            > PROGRAM-ID. FIRSTPGM
*>                            > ```
         DATA DIVISION.
         WORKING-STORAGE SECTION.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 FIRSTPGM/WS-A.
         01  WS-A                PIC X.
*>           ^^^^ definition scip-cobol cobol fixture 0.0.0 FIRSTPGM/WS-A.
*>                kind Variable
*>                display_name WS-A
*>                documentation
*>                > ```cobol
*>                > 01 WS-A PIC X
*>                > ```
*>                                    ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 FIRSTPGM/WS-A.
         PROCEDURE DIVISION.
             CALL 'SECONDPGM' USING WS-A
*>                 ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 SECONDPGM/
*>                                  ^^^^ reference scip-cobol cobol fixture 0.0.0 FIRSTPGM/WS-A.
             CALL 'ALTENTRY' USING WS-A
*>                 ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 SECONDPGM/ALTENTRY(entry).
*>                                 ^^^^ reference scip-cobol cobol fixture 0.0.0 FIRSTPGM/WS-A.
             STOP RUN.
*>                   ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 FIRSTPGM/
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 SECONDPGM/
         IDENTIFICATION DIVISION.
         PROGRAM-ID. SECONDPGM.
*>                   ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 SECONDPGM/
*>                             kind Module
*>                             display_name SECONDPGM
*>                             documentation
*>                             > ```cobol
*>                             > PROGRAM-ID. SECONDPGM
*>                             > ```
         DATA DIVISION.
         LINKAGE SECTION.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 SECONDPGM/LK-A.
         01  LK-A                PIC X.
*>           ^^^^ definition scip-cobol cobol fixture 0.0.0 SECONDPGM/LK-A.
*>                kind Variable
*>                display_name LK-A
*>                documentation
*>                > ```cobol
*>                > 01 LK-A PIC X
*>                > ```
*>                                    ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 SECONDPGM/LK-A.
         PROCEDURE DIVISION USING LK-A.
*>                                ^^^^ reference scip-cobol cobol fixture 0.0.0 SECONDPGM/LK-A.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 SECONDPGM/P1().
         P1.
*>       ^^ definition scip-cobol cobol fixture 0.0.0 SECONDPGM/P1().
*>          kind Function
*>          display_name P1
*>          documentation
*>          > ```cobol
*>          > P1.
*>          > ```
             MOVE 'X' TO LK-A
*>                       ^^^^ reference scip-cobol cobol fixture 0.0.0 SECONDPGM/LK-A.
             GOBACK.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 SECONDPGM/ALTENTRY(entry).
             ENTRY 'ALTENTRY' USING LK-A.
*>                  ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 SECONDPGM/ALTENTRY(entry).
*>                           kind Function
*>                           display_name ALTENTRY
*>                           documentation
*>                           > ```cobol
*>                           > ENTRY 'ALTENTRY' USING LK-A
*>                           > ```
*>                                  ^^^^ reference scip-cobol cobol fixture 0.0.0 SECONDPGM/LK-A.
*>                                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 SECONDPGM/ALTENTRY(entry).
             MOVE 'Y' TO LK-A
*>                       ^^^^ reference scip-cobol cobol fixture 0.0.0 SECONDPGM/LK-A.
             GOBACK.
*>                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 SECONDPGM/
*>                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 SECONDPGM/P1().
  
