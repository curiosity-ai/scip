         IDENTIFICATION DIVISION.
         PROGRAM-ID. CUSTMAIN.
*>                   ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 CUSTMAIN/
*>                            kind Module
*>                            display_name CUSTMAIN
*>                            documentation
*>                            > ```cobol
*>                            > PROGRAM-ID. CUSTMAIN
*>                            > ```
         DATA DIVISION.
         WORKING-STORAGE SECTION.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-IN.
         01  CUST-IN.
*>           ^^^^^^^ definition scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-IN.
*>                   kind Struct
*>                   display_name CUST-IN
*>                   documentation
*>                   > ```cobol
*>                   > 01 CUST-IN
*>                   > ```
*>                  ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-IN.
             COPY CUSTREC.
*>                ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-OUT.
         01  CUST-OUT.
*>           ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-OUT.
*>                    kind Struct
*>                    display_name CUST-OUT
*>                    documentation
*>                    > ```cobol
*>                    > 01 CUST-OUT
*>                    > ```
*>                   ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-OUT.
             COPY CUSTREC.
*>                ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/
             COPY TAGREC REPLACING ==:TAG:== BY ==ORD==.
*>                ^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/TAGREC.cpy`/
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 CUSTMAIN/WS-FLAGS.
         01  WS-FLAGS.
*>           ^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 CUSTMAIN/WS-FLAGS.
*>                    kind Struct
*>                    display_name WS-FLAGS
*>                    documentation
*>                    > ```cobol
*>                    > 01 WS-FLAGS
*>                    > ```
*>                   ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 CUSTMAIN/WS-FLAGS.
             COPY 'FLAGS' REPLACING ==(PFX)== BY ==WS==.
*>                 ^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/FLAGS.cpy`/
             COPY DATEFMT OF COMMONLIB.
*>                ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `commonlib/DATEFMT.cpy`/
             EXEC SQL INCLUDE SQLCA END-EXEC.
             EXEC SQL INCLUDE DCLCUST END-EXEC.
*>                            ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/
         REPLACE ==WS-OLD-NAME== BY ==WS-NEW-NAME==.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 CUSTMAIN/WS-NEW-NAME.
         01  WS-OLD-NAME         PIC X(10).
*>           ^^^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 CUSTMAIN/WS-NEW-NAME.
*>                       kind Variable
*>                       display_name WS-NEW-NAME
*>                       documentation
*>                       > ```cobol
*>                       > 01 WS-NEW-NAME PIC X(10)
*>                       > ```
*>                                        ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 CUSTMAIN/WS-NEW-NAME.
         REPLACE OFF.
         PROCEDURE DIVISION.
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 CUSTMAIN/MAIN-PARA().
         MAIN-PARA.
*>       ^^^^^^^^^ definition scip-cobol cobol fixture 0.0.0 CUSTMAIN/MAIN-PARA().
*>                 kind Function
*>                 display_name MAIN-PARA
*>                 documentation
*>                 > ```cobol
*>                 > MAIN-PARA.
*>                 > ```
             MOVE CUST-ID OF CUST-IN TO CUST-ID OF CUST-OUT
*>                ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-ID.
*>                           ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-IN.
*>                                      ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-ID.
*>                                                 ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-OUT.
             MOVE CUST-NAME IN CUST-OUT TO WS-NEW-NAME
*>                ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-NAME.
*>                             ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-OUT.
*>                                         ^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 CUSTMAIN/WS-NEW-NAME.
             MOVE ORD-ID TO DCL-ORDER-ID
*>                ^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/TAGREC.cpy`/ORD-REC.ORD-ID.
*>                          ^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.DCL-ORDER-ID.
             IF WS-FLAG-ON
*>              ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/FLAGS.cpy`/WS-FLAG.WS-FLAG-ON.
                MOVE DATE-YYYY TO ORD-YEAR
*>                   ^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `commonlib/DATEFMT.cpy`/DATE-FIELDS.DATE-YYYY.
*>                                ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/TAGREC.cpy`/ORD-REC.ORD-YEAR.
             END-IF
             EXEC SQL
                 SELECT NAME INTO :DCL-NAME
*>                                 ^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.DCL-NAME.
                 FROM CUSTOMER WHERE ID = :DCL-ORDER-ID
*>                                         ^^^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/DCLCUST.cpy`/DCLCUSTOMER.DCL-ORDER-ID.
             END-EXEC
             EXEC CICS LINK PROGRAM('CUSTSUB')
*>                                   ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 CUSTSUB/
                  COMMAREA(CUST-IN)
*>                         ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-IN.
                  LENGTH(LENGTH OF CUST-IN)
*>                                 ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-IN.
             END-EXEC
             EXEC CICS HANDLE CONDITION NOTFND(ERROR-EXIT) END-EXEC
*>                                             ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/ERRPARA.cpy`/ERROR-EXIT().
             COPY CHKPROC REPLACING ==(FLD)== BY ==CUST-ID==.
*>                ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/CHKPROC.cpy`/
             GOBACK.
*>                 ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 CUSTMAIN/MAIN-PARA().
             COPY ERRPARA.
*>                ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/ERRPARA.cpy`/
  
