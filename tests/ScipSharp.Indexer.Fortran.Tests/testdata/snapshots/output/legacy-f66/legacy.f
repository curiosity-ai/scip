 C     FORTRAN 66 STYLE: ARITHMETIC IF, COMPUTED AND ASSIGNED GO TO,
 C     HOLLERITH CONSTANTS, EQUIVALENCE, ALTERNATE RETURNS.
!      ⌄ enclosing_range_start scip-fortran . legacy-f66 . legacy/
       PROGRAM LEGACY
!              ^^^^^^ definition scip-fortran . legacy-f66 . legacy/
!                     kind Namespace
!                     display_name LEGACY
!                     signature_documentation
!                     > PROGRAM LEGACY
       INTEGER IA(4), IB(2)
!              ^^ definition local 0
!                 kind Variable
!                 display_name IA
!                 signature_documentation
!                 > INTEGER :: IA(4)
!                 enclosing_symbol scip-fortran . legacy-f66 . legacy/
!                     ^^ definition local 1
!                        kind Variable
!                        display_name IB
!                        signature_documentation
!                        > INTEGER :: IB(2)
!                        enclosing_symbol scip-fortran . legacy-f66 . legacy/
       REAL RX
!           ^^ definition local 2
!              kind Variable
!              display_name RX
!              signature_documentation
!              > REAL :: RX
!              enclosing_symbol scip-fortran . legacy-f66 . legacy/
       EQUIVALENCE (IA(1), IB(1)), (RX, IA(3))
!                   ^^ reference local 0
!                          ^^ reference local 1
!                                   ^^ reference local 2
!                                       ^^ reference local 0
       DATA IA /4*0/
!           ^^ reference local 0
       K = 2
!      ^ definition local 3
!        kind Variable
!        display_name K
!        signature_documentation
!        > integer :: K
!        enclosing_symbol scip-fortran . legacy-f66 . legacy/
!        documentation
!        > Implicitly typed.
       GO TO (100, 200, 300), K
!             ^^^ reference local 4
!                  ^^^ reference local 5
!                       ^^^ reference local 6
!                             ^ reference local 3
   100 IF (K - 2) 200, 300, 400
!  ^^^ definition local 4
!      display_name 100
!      signature_documentation
!      > 100 IF (K - 2) 200, 300, 400
!      enclosing_symbol scip-fortran . legacy-f66 . legacy/
!          ^ reference local 3
!                 ^^^ reference local 5
!                      ^^^ reference local 6
!                           ^^^ reference local 7
   200 ASSIGN 400 TO LBL
!  ^^^ definition local 5
!      display_name 200
!      signature_documentation
!      > 200 ASSIGN 400 TO LBL
!      enclosing_symbol scip-fortran . legacy-f66 . legacy/
!             ^^^ reference local 7
!                    ^^^ definition local 8
!                        kind Variable
!                        display_name LBL
!                        signature_documentation
!                        > integer :: LBL
!                        enclosing_symbol scip-fortran . legacy-f66 . legacy/
!                        documentation
!                        > Implicitly typed.
       GO TO LBL, (300, 400)
!            ^^^ reference local 8
!                  ^^^ reference local 6
!                       ^^^ reference local 7
   300 CALL CHECK(K, *400)
!  ^^^ definition local 6
!      display_name 300
!      signature_documentation
!      > 300 CALL CHECK(K, *400)
!      enclosing_symbol scip-fortran . legacy-f66 . legacy/
!           ^^^^^ reference scip-fortran . legacy-f66 . check().
!                 ^ reference local 3
!                     ^^^ reference local 7
   400 WRITE (6, 10)
!  ^^^ definition local 7
!      display_name 400
!      signature_documentation
!      > 400 WRITE (6, 10)
!      enclosing_symbol scip-fortran . legacy-f66 . legacy/
!                ^^ reference local 9
    10 FORMAT (5HHELLO, 2X, 5HWORLD)
!   ^^ definition local 9
!      display_name 10
!      signature_documentation
!      > 10 FORMAT (5HHELLO, 2X, 5HWORLD)
!      enclosing_symbol scip-fortran . legacy-f66 . legacy/
       PRINT 20, K
!            ^^ reference local 10
!                ^ reference local 3
    20 FORMAT(1X,I3)
!   ^^ definition local 10
!      display_name 20
!      signature_documentation
!      > 20 FORMAT(1X,I3)
!      enclosing_symbol scip-fortran . legacy-f66 . legacy/
       STOP
       END
!        ⌃ enclosing_range_end scip-fortran . legacy-f66 . legacy/
!      ⌄ enclosing_range_start scip-fortran . legacy-f66 . check().
       SUBROUTINE CHECK(N, *)
!                 ^^^^^ definition scip-fortran . legacy-f66 . check().
!                       kind Function
!                       display_name CHECK
!                       signature_documentation
!                       > SUBROUTINE CHECK(N, *)
!                       >   integer :: N
!                       ^ definition local 11
!                         kind Parameter
!                         display_name N
!                         signature_documentation
!                         > integer :: N
!                         enclosing_symbol scip-fortran . legacy-f66 . check().
       IF (N.GT.1) RETURN 1
!          ^ reference local 11
       RETURN
       END
!        ⌃ enclosing_range_end scip-fortran . legacy-f66 . check().
 
