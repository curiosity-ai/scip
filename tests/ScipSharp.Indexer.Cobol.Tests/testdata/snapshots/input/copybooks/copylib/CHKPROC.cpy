      * Statements copied into the procedure division.
           IF (FLD) OF CUST-IN = ZERO
              PERFORM ERROR-EXIT
           END-IF
