        $SET SOURCEFORMAT"FIXED"
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/
         IDENTIFICATION DIVISION.
         PROGRAM-ID. REPORTS.
*>                   ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/
*>                           kind Module
*>                           display_name REPORTS
*>                           documentation
*>                           > ```cobol
*>                           > PROGRAM-ID. REPORTS
*>                           > ```
         ENVIRONMENT DIVISION.
         INPUT-OUTPUT SECTION.
         FILE-CONTROL.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/SORT-WORK#
             SELECT SORT-WORK ASSIGN TO SORTWK1.
*>                  ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/SORT-WORK#
*>                            kind File
*>                            display_name SORT-WORK
*>                            documentation
*>                            > ```cobol
*>                            > SELECT SORT-WORK ASSIGN TO SORTWK1
*>                            > ```
*>                                             ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/SORT-WORK#
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/IN-FILE#
             SELECT IN-FILE ASSIGN TO INFILE
*>                  ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/IN-FILE#
*>                          kind File
*>                          display_name IN-FILE
*>                          documentation
*>                          > ```cobol
*>                          > SELECT IN-FILE ASSIGN TO INFILE RECORD KEY IS IN-KEY
*>                          > ```
                 RECORD KEY IS IN-KEY.
*>                             ^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.IN-KEY.
*>                                   ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/IN-FILE#
         DATA DIVISION.
         FILE SECTION.
         SD  SORT-WORK.
*>           ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/SORT-WORK#
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.
         01  SORT-REC.
*>           ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.
*>                    kind Struct
*>                    display_name SORT-REC
*>                    documentation
*>                    > ```cobol
*>                    > 01 SORT-REC
*>                    > ```
*>                    documentation
*>                    > Record of file `SORT-WORK`
*>                   ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.SORT-KEY.
             05 SORT-KEY             PIC X(8).
*>              ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.SORT-KEY.
*>                       kind Field
*>                       display_name SORT-KEY
*>                       documentation
*>                       > ```cobol
*>                       > 05 SORT-KEY PIC X(8)
*>                       > ```
*>                                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.SORT-KEY.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.SORT-DATA.
             05 SORT-DATA            PIC X(72).
*>              ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.SORT-DATA.
*>                        kind Field
*>                        display_name SORT-DATA
*>                        documentation
*>                        > ```cobol
*>                        > 05 SORT-DATA PIC X(72)
*>                        > ```
*>                                            ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.SORT-DATA.
         FD  IN-FILE
*>           ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/IN-FILE#
             DATA RECORD IS IN-REC.
*>                          ^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.
         01  IN-REC.
*>           ^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.
*>                  kind Struct
*>                  display_name IN-REC
*>                  documentation
*>                  > ```cobol
*>                  > 01 IN-REC
*>                  > ```
*>                  documentation
*>                  > Record of file `IN-FILE`
*>                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.IN-KEY.
             05 IN-KEY               PIC X(8).
*>              ^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.IN-KEY.
*>                     kind Field
*>                     display_name IN-KEY
*>                     documentation
*>                     > ```cobol
*>                     > 05 IN-KEY PIC X(8)
*>                     > ```
*>                                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.IN-KEY.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.IN-DATA.
             05 IN-DATA              PIC X(72).
*>              ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.IN-DATA.
*>                      kind Field
*>                      display_name IN-DATA
*>                      documentation
*>                      > ```cobol
*>                      > 05 IN-DATA PIC X(72)
*>                      > ```
*>                                            ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.IN-DATA.
         WORKING-STORAGE SECTION.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/MAX-LINES.
         78  MAX-LINES               VALUE 60.
*>           ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/MAX-LINES.
*>                     kind Constant
*>                     display_name MAX-LINES
*>                     documentation
*>                     > ```cobol
*>                     > 78 MAX-LINES VALUE 60
*>                     > ```
*>                                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/MAX-LINES.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.
         01  WS-DATE.
*>           ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.
*>                   kind Struct
*>                   display_name WS-DATE
*>                   documentation
*>                   > ```cobol
*>                   > 01 WS-DATE
*>                   > ```
*>                  ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-YEAR.
             05 WS-YEAR              PIC 9(4).
*>              ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-YEAR.
*>                      kind Field
*>                      display_name WS-YEAR
*>                      documentation
*>                      > ```cobol
*>                      > 05 WS-YEAR PIC 9(4)
*>                      > ```
*>                                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-YEAR.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-MONTH.
             05 WS-MONTH             PIC 99.
*>              ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-MONTH.
*>                       kind Field
*>                       display_name WS-MONTH
*>                       documentation
*>                       > ```cobol
*>                       > 05 WS-MONTH PIC 99
*>                       > ```
*>                                         ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-MONTH.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-DAY.
             05 WS-DAY               PIC 99.
*>              ^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-DAY.
*>                     kind Field
*>                     display_name WS-DAY
*>                     documentation
*>                     > ```cobol
*>                     > 05 WS-DAY PIC 99
*>                     > ```
*>                                         ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-DAY.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/WS-YEAR-MONTH.
         66  WS-YEAR-MONTH RENAMES WS-YEAR THRU WS-MONTH.
*>           ^^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/WS-YEAR-MONTH.
*>                         kind Field
*>                         display_name WS-YEAR-MONTH
*>                         documentation
*>                         > ```cobol
*>                         > 66 WS-YEAR-MONTH RENAMES WS-YEAR THRU WS-MONTH
*>                         > ```
*>                                 ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-YEAR.
*>                                              ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/WS-DATE.WS-MONTH.
*>                                                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/WS-YEAR-MONTH.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/WS-CHOICE.
         01  WS-CHOICE               PIC 9 VALUE 1.
*>           ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/WS-CHOICE.
*>                     kind Variable
*>                     display_name WS-CHOICE
*>                     documentation
*>                     > ```cobol
*>                     > 01 WS-CHOICE PIC 9 VALUE 1
*>                     > ```
*>                                                ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/WS-CHOICE.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/WS-KEY-OF-REC.
         01  WS-KEY-OF-REC           PIC X(8).
*>           ^^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/WS-KEY-OF-REC.
*>                         kind Variable
*>                         display_name WS-KEY-OF-REC
*>                         documentation
*>                         > ```cobol
*>                         > 01 WS-KEY-OF-REC PIC X(8)
*>                         > ```
*>                                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/WS-KEY-OF-REC.
             COPY LOOPA.
*>                ^^^^^ reference scip-cobol cobol fixture 0.0.0 `LOOPA.cpy`/
         PROCEDURE DIVISION.
         DECLARATIVES.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/IN-ERROR().
         IN-ERROR SECTION.
*>       ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/IN-ERROR().
*>                kind Namespace
*>                display_name IN-ERROR
*>                documentation
*>                > ```cobol
*>                > IN-ERROR SECTION
*>                > ```
             USE AFTER STANDARD ERROR PROCEDURE ON IN-FILE.
*>                                                 ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/IN-FILE#
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/IN-ERROR().IN-ERROR-PARA().
         IN-ERROR-PARA.
*>       ^^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/IN-ERROR().IN-ERROR-PARA().
*>                     kind Function
*>                     display_name IN-ERROR-PARA
*>                     documentation
*>                     > ```cobol
*>                     > IN-ERROR-PARA. (in section IN-ERROR)
*>                     > ```
             DISPLAY 'I/O ERROR ON IN-FILE'.
         END DECLARATIVES.
*>                       ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/IN-ERROR().
*>                       ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/IN-ERROR().IN-ERROR-PARA().
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().
         MAIN-LINE SECTION.
*>       ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().
*>                 kind Namespace
*>                 display_name MAIN-LINE
*>                 documentation
*>                 > ```cobol
*>                 > MAIN-LINE SECTION
*>                 > ```
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-100().
         M-100.
*>       ^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-100().
*>             kind Function
*>             display_name M-100
*>             documentation
*>             > ```cobol
*>             > M-100. (in section MAIN-LINE)
*>             > ```
             MOVE SORT-KEY OF SORT-REC TO WS-KEY-OF-REC
*>                ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.SORT-KEY.
*>                            ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.
*>                                        ^^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/WS-KEY-OF-REC.
             MOVE IN-KEY IN IN-REC OF IN-FILE TO WS-KEY-OF-REC
*>                ^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.IN-KEY.
*>                          ^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/IN-REC.
*>                                    ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/IN-FILE#
*>                                               ^^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/WS-KEY-OF-REC.
             SORT SORT-WORK ON ASCENDING KEY SORT-KEY
*>                ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/SORT-WORK#
*>                                           ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.SORT-KEY.
                 INPUT PROCEDURE IS LOAD-SORT THRU LOAD-SORT-EXIT
*>                                  ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().LOAD-SORT().
*>                                                 ^^^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().LOAD-SORT-EXIT().
                 OUTPUT PROCEDURE PRINT-SORTED
*>                                ^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().PRINT-SORTED().
             GO TO M-200 M-300 DEPENDING ON WS-CHOICE
*>                 ^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-200().
*>                       ^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-300().
*>                                          ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/WS-CHOICE.
             ALTER M-900 TO PROCEED TO M-300.
*>                 ^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-900().
*>                                     ^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-300().
*>                                          ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-100().
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-200().
         M-200.
*>       ^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-200().
*>             kind Function
*>             display_name M-200
*>             documentation
*>             > ```cobol
*>             > M-200. (in section MAIN-LINE)
*>             > ```
             DISPLAY WS-YEAR-MONTH MAX-LINES.
*>                   ^^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/WS-YEAR-MONTH.
*>                                 ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/MAX-LINES.
*>                                          ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-200().
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-300().
         M-300.
*>       ^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-300().
*>             kind Function
*>             display_name M-300
*>             documentation
*>             > ```cobol
*>             > M-300. (in section MAIN-LINE)
*>             > ```
             GOBACK.
*>                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-300().
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-900().
         M-900.
*>       ^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-900().
*>             kind Function
*>             display_name M-900
*>             documentation
*>             > ```cobol
*>             > M-900. (in section MAIN-LINE)
*>             > ```
             GO TO M-200.
*>                 ^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-200().
*>                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().M-900().
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().LOAD-SORT().
         LOAD-SORT.
*>       ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().LOAD-SORT().
*>                 kind Function
*>                 display_name LOAD-SORT
*>                 documentation
*>                 > ```cobol
*>                 > LOAD-SORT. (in section MAIN-LINE)
*>                 > ```
             RELEASE SORT-REC.
*>                   ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/SORT-REC.
*>                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().LOAD-SORT().
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().LOAD-SORT-EXIT().
         LOAD-SORT-EXIT.
*>       ^^^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().LOAD-SORT-EXIT().
*>                      kind Function
*>                      display_name LOAD-SORT-EXIT
*>                      documentation
*>                      > ```cobol
*>                      > LOAD-SORT-EXIT. (in section MAIN-LINE)
*>                      > ```
             EXIT.
*>               ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().LOAD-SORT-EXIT().
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().PRINT-SORTED().
         PRINT-SORTED.
*>       ^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().PRINT-SORTED().
*>                    kind Function
*>                    display_name PRINT-SORTED
*>                    documentation
*>                    > ```cobol
*>                    > PRINT-SORTED. (in section MAIN-LINE)
*>                    > ```
             RETURN SORT-WORK AT END CONTINUE END-RETURN.
*>                  ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 REPORTS/SORT-WORK#
*>                                                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/
*>                                                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().
*>                                                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 REPORTS/MAIN-LINE().PRINT-SORTED().
  
