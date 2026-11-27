        * Statements copied into the procedure division.
*>^ definition scip-cobol cobol fixture 0.0.0 `copylib/CHKPROC.cpy`/
*>  kind File
*>  display_name CHKPROC.cpy
*>  documentation
*>  > copybook `copylib/CHKPROC.cpy`
             IF (FLD) OF CUST-IN = ZERO
*>              ^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/CUSTREC.cpy`/CUST-ID.
*>                       ^^^^^^^ reference scip-cobol cobol fixture 0.0.0 CUSTMAIN/CUST-IN.
                PERFORM ERROR-EXIT
*>                      ^^^^^^^^^^ reference scip-cobol cobol fixture 0.0.0 `copylib/ERRPARA.cpy`/ERROR-EXIT().
             END-IF
  
