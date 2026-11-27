        * Copies LOOPB, which copies this copybook again: the cycle is cut.
*>^ definition scip-cobol cobol fixture 0.0.0 `LOOPA.cpy`/
*>  kind File
*>  display_name LOOPA.cpy
*>  documentation
*>  > copybook `LOOPA.cpy`
*>       ⌄ enclosing_range_start scip-cobol cobol fixture 0.0.0 `LOOPA.cpy`/LOOP-A.
         01  LOOP-A                  PIC X.
*>           ^^^^^^ definition scip-cobol cobol fixture 0.0.0 `LOOPA.cpy`/LOOP-A.
*>                  kind Variable
*>                  display_name LOOP-A
*>                  documentation
*>                  > ```cobol
*>                  > 01 LOOP-A PIC X
*>                  > ```
*>                                        ⌃ enclosing_range_end scip-cobol cobol fixture 0.0.0 `LOOPA.cpy`/LOOP-A.
             COPY LOOPB.
*>                ^^^^^ reference scip-cobol cobol fixture 0.0.0 `LOOPB.cpy`/
  
