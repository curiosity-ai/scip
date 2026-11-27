      * Two separately compiled programs in one file (no END PROGRAM),
      * plus an alternate ENTRY point.
       IDENTIFICATION DIVISION.
       PROGRAM-ID. FIRSTPGM.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01  WS-A                PIC X.
       PROCEDURE DIVISION.
           CALL 'SECONDPGM' USING WS-A
           CALL 'ALTENTRY' USING WS-A
           STOP RUN.
       IDENTIFICATION DIVISION.
       PROGRAM-ID. SECONDPGM.
       DATA DIVISION.
       LINKAGE SECTION.
       01  LK-A                PIC X.
       PROCEDURE DIVISION USING LK-A.
       P1.
           MOVE 'X' TO LK-A
           GOBACK.
           ENTRY 'ALTENTRY' USING LK-A.
           MOVE 'Y' TO LK-A
           GOBACK.
