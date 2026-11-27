*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 CUSTSUB/
         IDENTIFICATION DIVISION.
         PROGRAM-ID. CUSTSUB.
*>                   ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 CUSTSUB/
*>                           kind Module
*>                           display_name CUSTSUB
*>                           documentation
*>                           > ```cobol
*>                           > PROGRAM-ID. CUSTSUB
*>                           > ```
         DATA DIVISION.
         WORKING-STORAGE SECTION.
             COPY LOCALCPY.
*>                ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `src/LOCALCPY.cpy`/
         LINKAGE SECTION.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 CUSTSUB/DFHCOMMAREA.
         01  DFHCOMMAREA.
*>           ^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 CUSTSUB/DFHCOMMAREA.
*>                       kind Struct
*>                       display_name DFHCOMMAREA
*>                       documentation
*>                       > ```cobol
*>                       > 01 DFHCOMMAREA
*>                       > ```
*>                      ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 CUSTSUB/DFHCOMMAREA.
             COPY CUSTREC.
*>                ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/
         PROCEDURE DIVISION USING DFHCOMMAREA.
*>                                ^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 CUSTSUB/DFHCOMMAREA.
             MOVE CUST-NAME TO LOCAL-NAME
*>                ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-NAME.
*>                             ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `src/LOCALCPY.cpy`/LOCAL-STATE.LOCAL-NAME.
             ADD 1 TO LOCAL-CALLS
*>                    ^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `src/LOCALCPY.cpy`/LOCAL-STATE.LOCAL-CALLS.
             GOBACK.
*>                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 CUSTSUB/
  
