       IDENTIFICATION DIVISION.
       PROGRAM-ID. PAYCALC.
       DATA DIVISION.
       LINKAGE SECTION.
       01  LK-TOTALS.
           05 LK-TOTAL             PIC S9(9)V99.
           05 FILLER               PIC X(41).
       01  LK-COUNT                PIC 9(5).
       PROCEDURE DIVISION USING LK-TOTALS LK-COUNT.
           IF LK-COUNT > 0
              DIVIDE LK-COUNT INTO LK-TOTAL
           END-IF
           GOBACK.
