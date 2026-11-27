*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 OUTER/
         IDENTIFICATION DIVISION.
         PROGRAM-ID. OUTER.
*>                   ^^^^^ definition scip-cobol cobol fixture 0.0.0 OUTER/
*>                         kind Module
*>                         display_name OUTER
*>                         documentation
*>                         > ```cobol
*>                         > PROGRAM-ID. OUTER
*>                         > ```
        * Nested programs: GLOBAL data, COMMON programs, END PROGRAM.
         DATA DIVISION.
         WORKING-STORAGE SECTION.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 OUTER/SHARED-AREA.
         01  SHARED-AREA IS GLOBAL.
*>           ^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 OUTER/SHARED-AREA.
*>                       kind Struct
*>                       display_name SHARED-AREA
*>                       documentation
*>                       > ```cobol
*>                       > 01 SHARED-AREA IS GLOBAL
*>                       > ```
*>                                ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 OUTER/SHARED-AREA.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 OUTER/SHARED-AREA.SHARED-COUNT.
             05 SHARED-COUNT     PIC 9(4) VALUE 0.
*>              ^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 OUTER/SHARED-AREA.SHARED-COUNT.
*>                           kind Field
*>                           display_name SHARED-COUNT
*>                           documentation
*>                           > ```cobol
*>                           > 05 SHARED-COUNT PIC 9(4) VALUE 0
*>                           > ```
*>                                               ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 OUTER/SHARED-AREA.SHARED-COUNT.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 OUTER/PRIVATE-AREA.
         01  PRIVATE-AREA        PIC X(10).
*>           ^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 OUTER/PRIVATE-AREA.
*>                        kind Variable
*>                        display_name PRIVATE-AREA
*>                        documentation
*>                        > ```cobol
*>                        > 01 PRIVATE-AREA PIC X(10)
*>                        > ```
*>                                        ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 OUTER/PRIVATE-AREA.
         PROCEDURE DIVISION.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 OUTER/MAIN().
         MAIN SECTION.
*>       ^^^^ definition scip-cobol cobol fixture 0.0.0 OUTER/MAIN().
*>            kind Namespace
*>            display_name MAIN
*>            documentation
*>            > ```cobol
*>            > MAIN SECTION
*>            > ```
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 OUTER/MAIN().START-UP().
         START-UP.
*>       ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 OUTER/MAIN().START-UP().
*>                kind Function
*>                display_name START-UP
*>                documentation
*>                > ```cobol
*>                > START-UP. (in section MAIN)
*>                > ```
             CALL 'INNER-A'
*>                 ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 OUTER/INNER-A/
             CALL 'INNER-B'
*>                 ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 OUTER/INNER-B/
             PERFORM DONE OF MAIN
*>                   ^^^^ reference scip-cobol cobol fixture 0.0.0 OUTER/MAIN().DONE().
*>                           ^^^^ reference scip-cobol cobol fixture 0.0.0 OUTER/MAIN().
             GOBACK.
*>                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 OUTER/MAIN().START-UP().
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 OUTER/MAIN().DONE().
         DONE.
*>       ^^^^ definition scip-cobol cobol fixture 0.0.0 OUTER/MAIN().DONE().
*>            kind Function
*>            display_name DONE
*>            documentation
*>            > ```cobol
*>            > DONE. (in section MAIN)
*>            > ```
             EXIT.
*>               ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 OUTER/MAIN().
*>               ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 OUTER/MAIN().DONE().
  
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 OUTER/INNER-A/
         IDENTIFICATION DIVISION.
         PROGRAM-ID. INNER-A IS COMMON PROGRAM.
*>                   ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 OUTER/INNER-A/
*>                           kind Module
*>                           display_name INNER-A
*>                           documentation
*>                           > ```cobol
*>                           > PROGRAM-ID. INNER-A IS COMMON PROGRAM
*>                           > ```
*>                           documentation
*>                           > COMMON program
         DATA DIVISION.
         WORKING-STORAGE SECTION.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 OUTER/INNER-A/PRIVATE-AREA.
         01  PRIVATE-AREA        PIC X(20).
*>           ^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 OUTER/INNER-A/PRIVATE-AREA.
*>                        kind Variable
*>                        display_name PRIVATE-AREA
*>                        documentation
*>                        > ```cobol
*>                        > 01 PRIVATE-AREA PIC X(20)
*>                        > ```
*>                                        ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 OUTER/INNER-A/PRIVATE-AREA.
         PROCEDURE DIVISION.
             ADD 1 TO SHARED-COUNT
*>                    ^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 OUTER/SHARED-AREA.SHARED-COUNT.
             MOVE SPACES TO PRIVATE-AREA
*>                          ^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 OUTER/INNER-A/PRIVATE-AREA.
             GOBACK.
         END PROGRAM INNER-A.
*>                   ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 OUTER/INNER-A/
*>                          ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 OUTER/INNER-A/
  
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 OUTER/INNER-B/
         IDENTIFICATION DIVISION.
         PROGRAM-ID. INNER-B.
*>                   ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 OUTER/INNER-B/
*>                           kind Module
*>                           display_name INNER-B
*>                           documentation
*>                           > ```cobol
*>                           > PROGRAM-ID. INNER-B
*>                           > ```
         PROCEDURE DIVISION.
             CALL 'INNER-A'
*>                 ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 OUTER/INNER-A/
             GOBACK.
         END PROGRAM INNER-B.
*>                   ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 OUTER/INNER-B/
*>                          ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 OUTER/INNER-B/
         END PROGRAM OUTER.
*>                   ^^^^^ reference scip-cobol cobol fixture 0.0.0 OUTER/
*>                        ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 OUTER/
  
