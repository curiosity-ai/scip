      $SET SOURCEFORMAT"FIXED"
       IDENTIFICATION DIVISION.
       PROGRAM-ID. REPORTS.
       ENVIRONMENT DIVISION.
       INPUT-OUTPUT SECTION.
       FILE-CONTROL.
           SELECT SORT-WORK ASSIGN TO SORTWK1.
           SELECT IN-FILE ASSIGN TO INFILE
               RECORD KEY IS IN-KEY.
       DATA DIVISION.
       FILE SECTION.
       SD  SORT-WORK.
       01  SORT-REC.
           05 SORT-KEY             PIC X(8).
           05 SORT-DATA            PIC X(72).
       FD  IN-FILE
           DATA RECORD IS IN-REC.
       01  IN-REC.
           05 IN-KEY               PIC X(8).
           05 IN-DATA              PIC X(72).
       WORKING-STORAGE SECTION.
       78  MAX-LINES               VALUE 60.
       01  WS-DATE.
           05 WS-YEAR              PIC 9(4).
           05 WS-MONTH             PIC 99.
           05 WS-DAY               PIC 99.
       66  WS-YEAR-MONTH RENAMES WS-YEAR THRU WS-MONTH.
       01  WS-CHOICE               PIC 9 VALUE 1.
       01  WS-KEY-OF-REC           PIC X(8).
           COPY LOOPA.
       PROCEDURE DIVISION.
       DECLARATIVES.
       IN-ERROR SECTION.
           USE AFTER STANDARD ERROR PROCEDURE ON IN-FILE.
       IN-ERROR-PARA.
           DISPLAY 'I/O ERROR ON IN-FILE'.
       END DECLARATIVES.
       MAIN-LINE SECTION.
       M-100.
           MOVE SORT-KEY OF SORT-REC TO WS-KEY-OF-REC
           MOVE IN-KEY IN IN-REC OF IN-FILE TO WS-KEY-OF-REC
           SORT SORT-WORK ON ASCENDING KEY SORT-KEY
               INPUT PROCEDURE IS LOAD-SORT THRU LOAD-SORT-EXIT
               OUTPUT PROCEDURE PRINT-SORTED
           GO TO M-200 M-300 DEPENDING ON WS-CHOICE
           ALTER M-900 TO PROCEED TO M-300.
       M-200.
           DISPLAY WS-YEAR-MONTH MAX-LINES.
       M-300.
           GOBACK.
       M-900.
           GO TO M-200.
       LOAD-SORT.
           RELEASE SORT-REC.
       LOAD-SORT-EXIT.
           EXIT.
       PRINT-SORTED.
           RETURN SORT-WORK AT END CONTINUE END-RETURN.
