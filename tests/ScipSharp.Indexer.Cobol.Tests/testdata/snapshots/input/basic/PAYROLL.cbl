000100 IDENTIFICATION DIVISION.                                         PAYROLL
000200 PROGRAM-ID. PAYROLL.                                             PAYROLL
000300 AUTHOR. Written by the fixture author. This is a comment entry   PAYROLL
000400     that spans 'several lines and an unbalanced quote.           PAYROLL
000500 ENVIRONMENT DIVISION.                                            PAYROLL
000600 CONFIGURATION SECTION.                                           PAYROLL
000700 SPECIAL-NAMES.                                                   PAYROLL
000800     C01 IS TOP-OF-PAGE.                                          PAYROLL
000900 INPUT-OUTPUT SECTION.                                            PAYROLL
001000 FILE-CONTROL.                                                    PAYROLL
001100     SELECT EMP-FILE ASSIGN TO EMPFILE                            PAYROLL
001200         ORGANIZATION IS SEQUENTIAL                               PAYROLL
001300         FILE STATUS IS WS-EMP-STATUS.                            PAYROLL
001400 DATA DIVISION.                                                   PAYROLL
001500 FILE SECTION.                                                    PAYROLL
001600 FD  EMP-FILE.                                                    PAYROLL
001700 01  EMP-REC.                                                     PAYROLL
001800     05 EMP-ID               PIC 9(6).                            PAYROLL
001900     05 EMP-NAME             PIC X(30).                           PAYROLL
002000     05 EMP-SALARY           PIC S9(7)V99 COMP-3.                 PAYROLL
002100 WORKING-STORAGE SECTION.                                         PAYROLL
002200 01  WS-EMP-STATUS           PIC XX.                              PAYROLL
002300     88 EMP-OK               VALUE '00'.                          PAYROLL
002400     88 EMP-EOF              VALUE '10'.                          PAYROLL
002500 77  WS-COUNT                PIC 9(5) VALUE ZERO.                 PAYROLL
002600 01  WS-TOTALS.                                                   PAYROLL
002700     05 WS-TOTAL             PIC S9(9)V99 VALUE 0.                PAYROLL
002800     05 WS-TOTAL-X REDEFINES WS-TOTAL PIC X(11).                  PAYROLL
002900     05 WS-NAME              PIC X(30).                           PAYROLL
003000 01  WS-OUT.                                                      PAYROLL
003100     05 WS-NAME              PIC X(30).                           PAYROLL
003200     05 WS-TABLE OCCURS 10 TIMES INDEXED BY WS-IDX.               PAYROLL
003300        10 WS-ENTRY          PIC X(4).                            PAYROLL
003400 01  WS-MESSAGE              PIC X(60) VALUE 'A LONG MESSAGE THAT PAYROLL
003500-    'IS CONTINUED'.                                              PAYROLL
003600 PROCEDURE DIVISION.                                              PAYROLL
003700 MAIN-LOGIC SECTION.                                              PAYROLL
003800 0000-MAIN.                                                       PAYROLL
003900     OPEN INPUT EMP-FILE                                          PAYROLL
004000     PERFORM 1000-READ THRU 1000-EXIT                             PAYROLL
004100     PERFORM UNTIL EMP-EOF                                        PAYROLL
004200         ADD EMP-SALARY TO WS-TOTAL                               PAYROLL
004300         MOVE EMP-NAME TO WS-NAME OF WS-OUT                       PAYROLL
004400         MOVE EMP-NAME TO WS-NAME IN WS-TOTALS                    PAYROLL
004500         SET WS-IDX TO 1                                          PAYROLL
004600         MOVE EMP-ID TO WS-ENTRY (WS-IDX)                         PAYROLL
004700         ADD 1 TO WS-COUNT                                        PAYROLL
004800         PERFORM 1000-READ THRU 1000-EXIT                         PAYROLL
004900     END-PERFORM                                                  PAYROLL
005000     CALL 'PAYCALC' USING WS-TOTALS, WS-COUNT                     PAYROLL
005100     CALL 'MISSING' USING WS-COUNT                                PAYROLL
005200     CLOSE EMP-FILE                                               PAYROLL
005300     GO TO 9999-END.                                              PAYROLL
005400 1000-READ.                                                       PAYROLL
005500     READ EMP-FILE                                                PAYROLL
005600         AT END SET EMP-EOF TO TRUE                               PAYROLL
005700     END-READ.                                                    PAYROLL
005800 1000-EXIT.                                                       PAYROLL
005900     EXIT.                                                        PAYROLL
006000*    A COMMENT LINE MENTIONING WS-COUNT                           PAYROLL
006100D    DISPLAY WS-COUNT                                             PAYROLL
006200 9999-END.                                                        PAYROLL
006300     DISPLAY 'TOTAL ' WS-TOTAL UPON CONSOLE                       PAYROLL
006400     STOP RUN.                                                    PAYROLL
