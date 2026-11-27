*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/
  000100 IDENTIFICATION DIVISION.                                         PAYROLL
  000200 PROGRAM-ID. PAYROLL.                                             PAYROLL
*>                   ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/
*>                           kind Module
*>                           display_name PAYROLL
*>                           documentation
*>                           > ```cobol
*>                           > PROGRAM-ID. PAYROLL
*>                           > ```
  000300 AUTHOR. Written by the fixture author. This is a comment entry   PAYROLL
  000400     that spans 'several lines and an unbalanced quote.           PAYROLL
  000500 ENVIRONMENT DIVISION.                                            PAYROLL
  000600 CONFIGURATION SECTION.                                           PAYROLL
  000700 SPECIAL-NAMES.                                                   PAYROLL
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/TOP-OF-PAGE.
  000800     C01 IS TOP-OF-PAGE.                                          PAYROLL
*>                  ^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/TOP-OF-PAGE.
*>                              kind Constant
*>                              display_name TOP-OF-PAGE
*>                              documentation
*>                              > ```cobol
*>                              > C01 IS TOP-OF-PAGE
*>                              > ```
*>                            ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/TOP-OF-PAGE.
  000900 INPUT-OUTPUT SECTION.                                            PAYROLL
  001000 FILE-CONTROL.                                                    PAYROLL
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-FILE#
  001100     SELECT EMP-FILE ASSIGN TO EMPFILE                            PAYROLL
*>                  ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-FILE#
*>                           kind File
*>                           display_name EMP-FILE
*>                           documentation
*>                           > ```cobol
*>                           > SELECT EMP-FILE ASSIGN TO EMPFILE ORGANIZATION IS SEQUENTIAL FILE STATUS IS WS-EMP-STATUS
*>                           > ```
  001200         ORGANIZATION IS SEQUENTIAL                               PAYROLL
  001300         FILE STATUS IS WS-EMP-STATUS.                            PAYROLL
*>                              ^^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.
*>                                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-FILE#
  001400 DATA DIVISION.                                                   PAYROLL
  001500 FILE SECTION.                                                    PAYROLL
  001600 FD  EMP-FILE.                                                    PAYROLL
*>           ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-FILE#
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.
  001700 01  EMP-REC.                                                     PAYROLL
*>           ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.
*>                   kind Struct
*>                   display_name EMP-REC
*>                   documentation
*>                   > ```cobol
*>                   > 01 EMP-REC
*>                   > ```
*>                   documentation
*>                   > Record of file `EMP-FILE`
*>                  ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-ID.
  001800     05 EMP-ID               PIC 9(6).                            PAYROLL
*>              ^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-ID.
*>                     kind Field
*>                     display_name EMP-ID
*>                     documentation
*>                     > ```cobol
*>                     > 05 EMP-ID PIC 9(6)
*>                     > ```
*>                                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-ID.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-NAME.
  001900     05 EMP-NAME             PIC X(30).                           PAYROLL
*>              ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-NAME.
*>                       kind Field
*>                       display_name EMP-NAME
*>                       documentation
*>                       > ```cobol
*>                       > 05 EMP-NAME PIC X(30)
*>                       > ```
*>                                            ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-NAME.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-SALARY.
  002000     05 EMP-SALARY           PIC S9(7)V99 COMP-3.                 PAYROLL
*>              ^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-SALARY.
*>                         kind Field
*>                         display_name EMP-SALARY
*>                         documentation
*>                         > ```cobol
*>                         > 05 EMP-SALARY PIC S9(7)V99 COMP-3
*>                         > ```
*>                                                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-SALARY.
  002100 WORKING-STORAGE SECTION.                                         PAYROLL
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.
  002200 01  WS-EMP-STATUS           PIC XX.                              PAYROLL
*>           ^^^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.
*>                         kind Variable
*>                         display_name WS-EMP-STATUS
*>                         documentation
*>                         > ```cobol
*>                         > 01 WS-EMP-STATUS PIC XX
*>                         > ```
*>                                         ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.EMP-OK.
  002300     88 EMP-OK               VALUE '00'.                          PAYROLL
*>              ^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.EMP-OK.
*>                     kind Constant
*>                     display_name EMP-OK
*>                     documentation
*>                     > ```cobol
*>                     > 88 EMP-OK VALUE '00'
*>                     > ```
*>                                             ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.EMP-OK.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.EMP-EOF.
  002400     88 EMP-EOF              VALUE '10'.                          PAYROLL
*>              ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.EMP-EOF.
*>                      kind Constant
*>                      display_name EMP-EOF
*>                      documentation
*>                      > ```cobol
*>                      > 88 EMP-EOF VALUE '10'
*>                      > ```
*>                                             ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.EMP-EOF.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-COUNT.
  002500 77  WS-COUNT                PIC 9(5) VALUE ZERO.                 PAYROLL
*>           ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-COUNT.
*>                    kind Variable
*>                    display_name WS-COUNT
*>                    documentation
*>                    > ```cobol
*>                    > 77 WS-COUNT PIC 9(5) VALUE ZERO
*>                    > ```
*>                                                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-COUNT.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.
  002600 01  WS-TOTALS.                                                   PAYROLL
*>           ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.
*>                     kind Struct
*>                     display_name WS-TOTALS
*>                     documentation
*>                     > ```cobol
*>                     > 01 WS-TOTALS
*>                     > ```
*>                    ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-TOTAL.
  002700     05 WS-TOTAL             PIC S9(9)V99 VALUE 0.                PAYROLL
*>              ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-TOTAL.
*>                       kind Field
*>                       display_name WS-TOTAL
*>                       documentation
*>                       > ```cobol
*>                       > 05 WS-TOTAL PIC S9(9)V99 VALUE 0
*>                       > ```
*>                                                       ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-TOTAL.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-TOTAL-X.
  002800     05 WS-TOTAL-X REDEFINES WS-TOTAL PIC X(11).                  PAYROLL
*>              ^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-TOTAL-X.
*>                         kind Field
*>                         display_name WS-TOTAL-X
*>                         documentation
*>                         > ```cobol
*>                         > 05 WS-TOTAL-X REDEFINES WS-TOTAL PIC X(11)
*>                         > ```
*>                                   ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-TOTAL.
*>                                                     ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-TOTAL-X.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-NAME.
  002900     05 WS-NAME              PIC X(30).                           PAYROLL
*>              ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-NAME.
*>                      kind Field
*>                      display_name WS-NAME
*>                      documentation
*>                      > ```cobol
*>                      > 05 WS-NAME PIC X(30)
*>                      > ```
*>                                            ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-NAME.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.
  003000 01  WS-OUT.                                                      PAYROLL
*>           ^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.
*>                  kind Struct
*>                  display_name WS-OUT
*>                  documentation
*>                  > ```cobol
*>                  > 01 WS-OUT
*>                  > ```
*>                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-NAME.
  003100     05 WS-NAME              PIC X(30).                           PAYROLL
*>              ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-NAME.
*>                      kind Field
*>                      display_name WS-NAME
*>                      documentation
*>                      > ```cobol
*>                      > 05 WS-NAME PIC X(30)
*>                      > ```
*>                                            ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-NAME.
*>           ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.
*>                                                  ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.WS-IDX.
  003200     05 WS-TABLE OCCURS 10 TIMES INDEXED BY WS-IDX.               PAYROLL
*>              ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.
*>                       kind Struct
*>                       display_name WS-TABLE
*>                       documentation
*>                       > ```cobol
*>                       > 05 WS-TABLE OCCURS 10 TIMES INDEXED BY WS-IDX
*>                       > ```
*>                                                  ^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.WS-IDX.
*>                                                         kind Variable
*>                                                         display_name WS-IDX
*>                                                         documentation
*>                                                         > ```cobol
*>                                                         > INDEXED BY WS-IDX (index of WS-TABLE)
*>                                                         > ```
*>                                                       ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.WS-IDX.
*>                                                        ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.
*>              ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.WS-ENTRY.
  003300        10 WS-ENTRY          PIC X(4).                            PAYROLL
*>                 ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.WS-ENTRY.
*>                          kind Field
*>                          display_name WS-ENTRY
*>                          documentation
*>                          > ```cobol
*>                          > 10 WS-ENTRY PIC X(4)
*>                          > ```
*>                                           ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.WS-ENTRY.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/WS-MESSAGE.
  003400 01  WS-MESSAGE              PIC X(60) VALUE 'A LONG MESSAGE THAT PAYROLL
*>           ^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/WS-MESSAGE.
*>                      kind Variable
*>                      display_name WS-MESSAGE
*>                      documentation
*>                      > ```cobol
*>                      > 01 WS-MESSAGE PIC X(60) VALUE 'A LONG MESSAGE THAT IS CONTINUED'
*>                      > ```
  003500-    'IS CONTINUED'.                                              PAYROLL
*>                         ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/WS-MESSAGE.
  003600 PROCEDURE DIVISION.                                              PAYROLL
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().
  003700 MAIN-LOGIC SECTION.                                              PAYROLL
*>       ^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().
*>                  kind Namespace
*>                  display_name MAIN-LOGIC
*>                  documentation
*>                  > ```cobol
*>                  > MAIN-LOGIC SECTION
*>                  > ```
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().0000-MAIN().
  003800 0000-MAIN.                                                       PAYROLL
*>       ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().0000-MAIN().
*>                 kind Function
*>                 display_name 0000-MAIN
*>                 documentation
*>                 > ```cobol
*>                 > 0000-MAIN. (in section MAIN-LOGIC)
*>                 > ```
  003900     OPEN INPUT EMP-FILE                                          PAYROLL
*>                      ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-FILE#
  004000     PERFORM 1000-READ THRU 1000-EXIT                             PAYROLL
*>                   ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().1000-READ().
*>                                  ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().1000-EXIT().
  004100     PERFORM UNTIL EMP-EOF                                        PAYROLL
*>                         ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.EMP-EOF.
  004200         ADD EMP-SALARY TO WS-TOTAL                               PAYROLL
*>                   ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-SALARY.
*>                                 ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-TOTAL.
  004300         MOVE EMP-NAME TO WS-NAME OF WS-OUT                       PAYROLL
*>                    ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-NAME.
*>                                ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-NAME.
*>                                           ^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.
  004400         MOVE EMP-NAME TO WS-NAME IN WS-TOTALS                    PAYROLL
*>                    ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-NAME.
*>                                ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-NAME.
*>                                           ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.
  004500         SET WS-IDX TO 1                                          PAYROLL
*>                   ^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.WS-IDX.
  004600         MOVE EMP-ID TO WS-ENTRY (WS-IDX)                         PAYROLL
*>                    ^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-REC.EMP-ID.
*>                              ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.WS-ENTRY.
*>                                        ^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-OUT.WS-TABLE.WS-IDX.
  004700         ADD 1 TO WS-COUNT                                        PAYROLL
*>                        ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-COUNT.
  004800         PERFORM 1000-READ THRU 1000-EXIT                         PAYROLL
*>                       ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().1000-READ().
*>                                      ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().1000-EXIT().
  004900     END-PERFORM                                                  PAYROLL
  005000     CALL 'PAYCALC' USING WS-TOTALS, WS-COUNT                     PAYROLL
*>                 ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYCALC/
*>                                ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.
*>                                           ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-COUNT.
  005100     CALL 'MISSING' USING WS-COUNT                                PAYROLL
*>                 ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 MISSING/
*>                                ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-COUNT.
  005200     CLOSE EMP-FILE                                               PAYROLL
*>                 ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-FILE#
  005300     GO TO 9999-END.                                              PAYROLL
*>                 ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().9999-END().
*>                         ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().0000-MAIN().
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().1000-READ().
  005400 1000-READ.                                                       PAYROLL
*>       ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().1000-READ().
*>                 kind Function
*>                 display_name 1000-READ
*>                 documentation
*>                 > ```cobol
*>                 > 1000-READ. (in section MAIN-LOGIC)
*>                 > ```
  005500     READ EMP-FILE                                                PAYROLL
*>                ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/EMP-FILE#
  005600         AT END SET EMP-EOF TO TRUE                               PAYROLL
*>                          ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-EMP-STATUS.EMP-EOF.
  005700     END-READ.                                                    PAYROLL
*>                   ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().1000-READ().
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().1000-EXIT().
  005800 1000-EXIT.                                                       PAYROLL
*>       ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().1000-EXIT().
*>                 kind Function
*>                 display_name 1000-EXIT
*>                 documentation
*>                 > ```cobol
*>                 > 1000-EXIT. (in section MAIN-LOGIC)
*>                 > ```
  005900     EXIT.                                                        PAYROLL
*>               ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().1000-EXIT().
  006000*    A COMMENT LINE MENTIONING WS-COUNT                           PAYROLL
  006100D    DISPLAY WS-COUNT                                             PAYROLL
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().9999-END().
  006200 9999-END.                                                        PAYROLL
*>       ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().9999-END().
*>                kind Function
*>                display_name 9999-END
*>                documentation
*>                > ```cobol
*>                > 9999-END. (in section MAIN-LOGIC)
*>                > ```
  006300     DISPLAY 'TOTAL ' WS-TOTAL UPON CONSOLE                       PAYROLL
*>                            ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 PAYROLL/WS-TOTALS.WS-TOTAL.
  006400     STOP RUN.                                                    PAYROLL
*>                   ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/
*>                   ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().
*>                   ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 PAYROLL/MAIN-LOGIC().9999-END().
  
