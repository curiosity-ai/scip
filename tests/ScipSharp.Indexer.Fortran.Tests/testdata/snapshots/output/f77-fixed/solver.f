 *> \brief SOLVE runs a toy Gauss-Seidel sweep.
 *>
 *> \verbatim
 *>  Uses the shared /WORK/ common block.
 *> \endverbatim
 C     ==================================================================
!      ⌄ enclosing_range_start scip-fortran . f77-fixed . solve/
       PROGRAM SOLVE
!              ^^^^^ definition scip-fortran . f77-fixed . solve/
!                    kind Namespace
!                    display_name SOLVE
!                    signature_documentation
!                    > PROGRAM SOLVE
!                    documentation
!                    > SOLVE runs a toy Gauss-Seidel sweep.
!                    > 
!                    >  Uses the shared /WORK/ common block.
       IMPLICIT DOUBLE PRECISION (A-H,O-Z)
       INCLUDE 'params.inc'
       DIMENSION X(NMAX)
!                ^ definition local 0
!                  kind Variable
!                  display_name X
!                  signature_documentation
!                  > double precision, dimension :: X(NMAX)
!                  enclosing_symbol scip-fortran . f77-fixed . solve/
!                  ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
       COMMON /WORK/ A(NMAX,NMAX), B(NMAX), ITERS
!              ^^^^ definition scip-fortran . f77-fixed . `/work/`#
!                   kind Struct
!                   display_name /WORK/
!                   signature_documentation
!                   > common /WORK/
!                    ^ definition scip-fortran . f77-fixed . `/work/`#a.
!                      kind StaticField
!                      display_name A
!                      signature_documentation
!                      > double precision :: A(NMAX,NMAX)
!                      ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                           ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                                  ^ definition scip-fortran . f77-fixed . `/work/`#b.
!                                    kind StaticField
!                                    display_name B
!                                    signature_documentation
!                                    > double precision :: B(NMAX)
!                                    ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                                           ^^^^^ definition scip-fortran . f77-fixed . `/work/`#iters.
!                                                 kind StaticField
!                                                 display_name ITERS
!                                                 signature_documentation
!                                                 > integer :: ITERS
       EXTERNAL RESID
!               ^^^^^ reference scip-fortran . f77-fixed . resid().
       DOUBLE PRECISION RESID
!                       ^^^^^ reference scip-fortran . f77-fixed . resid().
       ABS2(Z) = Z*Z
!      ^^^^ definition local 1
!           kind Function
!           display_name ABS2
!           signature_documentation
!           > ABS2(Z) = Z*Z
!           enclosing_symbol scip-fortran . f77-fixed . solve/
!           ^ definition local 2
!             kind Variable
!             display_name Z
!             signature_documentation
!             > double precision :: Z
!             enclosing_symbol scip-fortran . f77-fixed . solve/
!             documentation
!             > Implicitly typed.
!                ^ reference local 2
!                  ^ reference local 2
 C
       CALL SETUP(N)
!           ^^^^^ reference scip-fortran . f77-fixed . setup().
!                 ^ definition local 3
!                   kind Variable
!                   display_name N
!                   signature_documentation
!                   > integer :: N
!                   enclosing_symbol scip-fortran . f77-fixed . solve/
!                   documentation
!                   > Implicitly typed.
       DO 10 I = 1, N
!         ^^ reference local 4
!            ^ definition local 5
!              kind Variable
!              display_name I
!              signature_documentation
!              > integer :: I
!              enclosing_symbol scip-fortran . f77-fixed . solve/
!              documentation
!              > Implicitly typed.
!                   ^ reference local 3
          X(I) = 0.0D0
!         ^ reference local 0
!           ^ reference local 5
    10 CONTINUE
!   ^^ definition local 4
!      display_name 10
!      signature_documentation
!      > 10 CONTINUE
!      enclosing_symbol scip-fortran . f77-fixed . solve/
       DO 30 K = 1, 100
!         ^^ reference local 6
!            ^ definition local 7
!              kind Variable
!              display_name K
!              signature_documentation
!              > integer :: K
!              enclosing_symbol scip-fortran . f77-fixed . solve/
!              documentation
!              > Implicitly typed.
          CALL SWEEP(N, X)
!              ^^^^^ reference scip-fortran . f77-fixed . sweep().
!                    ^ reference local 3
!                       ^ reference local 0
          R = RESID(N, X)
!         ^ definition local 8
!           kind Variable
!           display_name R
!           signature_documentation
!           > double precision :: R
!           enclosing_symbol scip-fortran . f77-fixed . solve/
!           documentation
!           > Implicitly typed.
!             ^^^^^ reference scip-fortran . f77-fixed . resid().
!                   ^ reference local 3
!                      ^ reference local 0
          IF (ABS2(R) .LT. TOL*TOL) GO TO 40
!             ^^^^ reference local 1
!                  ^ reference local 8
!                          ^^^ reference scip-fortran . f77-fixed . `params.inc`/tol.
!                              ^^^ reference scip-fortran . f77-fixed . `params.inc`/tol.
!                                         ^^ reference local 9
    30 CONTINUE
!   ^^ definition local 6
!      display_name 30
!      signature_documentation
!      > 30 CONTINUE
!      enclosing_symbol scip-fortran . f77-fixed . solve/
    40 WRITE (6, 900) K, R
!   ^^ definition local 9
!      display_name 40
!      signature_documentation
!      > 40 WRITE (6, 900) K, R
!      enclosing_symbol scip-fortran . f77-fixed . solve/
!                ^^^ reference local 10
!                     ^ reference local 7
!                        ^ reference local 8
   900 FORMAT (' ITERATIONS ', I5, ' RESIDUAL ', 1PE12.4)
!  ^^^ definition local 10
!      display_name 900
!      signature_documentation
!      > 900 FORMAT (' ITERATIONS ', I5, ' RESIDUAL ', 1PE12.4)
!      enclosing_symbol scip-fortran . f77-fixed . solve/
       STOP
       END
!        ⌃ enclosing_range_end scip-fortran . f77-fixed . solve/
 C
!      ⌄ enclosing_range_start scip-fortran . f77-fixed . setup().
       SUBROUTINE SETUP(N)
!                 ^^^^^ definition scip-fortran . f77-fixed . setup().
!                       kind Function
!                       display_name SETUP
!                       signature_documentation
!                       > SUBROUTINE SETUP(N)
!                       >   integer :: N
!                       ^ definition local 11
!                         kind Parameter
!                         display_name N
!                         signature_documentation
!                         > integer :: N
!                         enclosing_symbol scip-fortran . f77-fixed . setup().
       INCLUDE 'params.inc'
       COMMON /WORK/ A(NMAX,NMAX), B(NMAX), ITERS
!              ^^^^ definition scip-fortran . f77-fixed . `/work/`#
!                   kind Struct
!                   display_name /WORK/
!                   signature_documentation
!                   > common /WORK/
!                    ^ definition scip-fortran . f77-fixed . `/work/`#a.
!                      kind StaticField
!                      display_name A
!                      signature_documentation
!                      > double precision :: A(NMAX,NMAX)
!                      ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                           ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                                  ^ definition scip-fortran . f77-fixed . `/work/`#b.
!                                    kind StaticField
!                                    display_name B
!                                    signature_documentation
!                                    > double precision :: B(NMAX)
!                                    ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                                           ^^^^^ definition scip-fortran . f77-fixed . `/work/`#iters.
!                                                 kind StaticField
!                                                 display_name ITERS
!                                                 signature_documentation
!                                                 > integer :: ITERS
       N = NMAX
!      ^ reference local 11
!          ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
       DO 20 I = 1, N
!         ^^ reference local 12
!            ^ definition local 13
!              kind Variable
!              display_name I
!              signature_documentation
!              > integer :: I
!              enclosing_symbol scip-fortran . f77-fixed . setup().
!              documentation
!              > Implicitly typed.
!                   ^ reference local 11
          DO 20 J = 1, N
!            ^^ reference local 12
!               ^ definition local 14
!                 kind Variable
!                 display_name J
!                 signature_documentation
!                 > integer :: J
!                 enclosing_symbol scip-fortran . f77-fixed . setup().
!                 documentation
!                 > Implicitly typed.
!                      ^ reference local 11
             A(I,J) = 1.0D0/DBLE(I+J-1)
!            ^ reference scip-fortran . f77-fixed . `/work/`#a.
!              ^ reference local 13
!                ^ reference local 14
!                                ^ reference local 13
!                                  ^ reference local 14
             IF (I .EQ. J) A(I,J) = A(I,J) + DBLE(N)
!                ^ reference local 13
!                       ^ reference local 14
!                          ^ reference scip-fortran . f77-fixed . `/work/`#a.
!                            ^ reference local 13
!                              ^ reference local 14
!                                   ^ reference scip-fortran . f77-fixed . `/work/`#a.
!                                     ^ reference local 13
!                                       ^ reference local 14
!                                                 ^ reference local 11
    20 CONTINUE
!   ^^ definition local 12
!      display_name 20
!      signature_documentation
!      > 20 CONTINUE
!      enclosing_symbol scip-fortran . f77-fixed . setup().
       CALLFILLB(N,
!          ^^^^^ reference scip-fortran . f77-fixed . fillb().
!                ^ reference local 11
      $          B)
!                ^ reference scip-fortran . f77-fixed . `/work/`#b.
       RETURN
       END
!        ⌃ enclosing_range_end scip-fortran . f77-fixed . setup().
 C
!      ⌄ enclosing_range_start scip-fortran . f77-fixed . fillb().
       SUBROUTINE FILLB(N, B)
!                 ^^^^^ definition scip-fortran . f77-fixed . fillb().
!                       kind Function
!                       display_name FILLB
!                       signature_documentation
!                       > SUBROUTINE FILLB(N, B)
!                       >   integer :: N
!                       >   DOUBLE PRECISION :: B(*)
!                       ^ definition local 15
!                         kind Parameter
!                         display_name N
!                         signature_documentation
!                         > integer :: N
!                         enclosing_symbol scip-fortran . f77-fixed . fillb().
!                          ^ definition local 16
!                            kind Parameter
!                            display_name B
!                            signature_documentation
!                            > DOUBLE PRECISION :: B(*)
!                            enclosing_symbol scip-fortran . f77-fixed . fillb().
       DOUBLE PRECISION B(*)
!                       ^ definition local 16
!                         kind Parameter
!                         display_name B
!                         signature_documentation
!                         > DOUBLE PRECISION :: B(*)
!                         enclosing_symbol scip-fortran . f77-fixed . fillb().
       DO 5 I = 1, N
!         ^ reference local 17
!           ^ definition local 18
!             kind Variable
!             display_name I
!             signature_documentation
!             > integer :: I
!             enclosing_symbol scip-fortran . f77-fixed . fillb().
!             documentation
!             > Implicitly typed.
!                  ^ reference local 15
     5 B(I) = 1.0D0
!    ^ definition local 17
!      display_name 5
!      signature_documentation
!      > 5 B(I) = 1.0D0
!      enclosing_symbol scip-fortran . f77-fixed . fillb().
!      ^ reference local 16
!        ^ reference local 18
       RETURN
       ENTRY FILLB0(N, B)
!            ^^^^^^ definition scip-fortran . f77-fixed . fillb0().
!                   kind Function
!                   display_name FILLB0
!                   signature_documentation
!                   > ENTRY FILLB0(N, B)
!                   >   integer :: N
!                   >   DOUBLE PRECISION :: B(*)
!                   ^ definition local 15
!                     kind Parameter
!                     display_name N
!                     signature_documentation
!                     > integer :: N
!                     enclosing_symbol scip-fortran . f77-fixed . fillb().
!                      ^ definition local 16
!                        kind Parameter
!                        display_name B
!                        signature_documentation
!                        > DOUBLE PRECISION :: B(*)
!                        enclosing_symbol scip-fortran . f77-fixed . fillb().
       B(1) = 0.0D0
!      ^ reference local 16
       END
!        ⌃ enclosing_range_end scip-fortran . f77-fixed . fillb().
 C
!      ⌄ enclosing_range_start scip-fortran . f77-fixed . sweep().
       SUBROUTINE SWEEP(N, X)
!                 ^^^^^ definition scip-fortran . f77-fixed . sweep().
!                       kind Function
!                       display_name SWEEP
!                       signature_documentation
!                       > SUBROUTINE SWEEP(N, X)
!                       >   integer :: N
!                       >   DOUBLE PRECISION :: X(N)
!                       ^ definition local 19
!                         kind Parameter
!                         display_name N
!                         signature_documentation
!                         > integer :: N
!                         enclosing_symbol scip-fortran . f77-fixed . sweep().
!                          ^ definition local 20
!                            kind Parameter
!                            display_name X
!                            signature_documentation
!                            > DOUBLE PRECISION :: X(N)
!                            enclosing_symbol scip-fortran . f77-fixed . sweep().
       INCLUDE 'params.inc'
       DOUBLE PRECISION X(N)
!                       ^ definition local 20
!                         kind Parameter
!                         display_name X
!                         signature_documentation
!                         > DOUBLE PRECISION :: X(N)
!                         enclosing_symbol scip-fortran . f77-fixed . sweep().
!                         ^ reference local 19
       COMMON /WORK/ A(NMAX,NMAX), B(NMAX), ITERS
!              ^^^^ definition scip-fortran . f77-fixed . `/work/`#
!                   kind Struct
!                   display_name /WORK/
!                   signature_documentation
!                   > common /WORK/
!                    ^ definition scip-fortran . f77-fixed . `/work/`#a.
!                      kind StaticField
!                      display_name A
!                      signature_documentation
!                      > double precision :: A(NMAX,NMAX)
!                      ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                           ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                                  ^ definition scip-fortran . f77-fixed . `/work/`#b.
!                                    kind StaticField
!                                    display_name B
!                                    signature_documentation
!                                    > double precision :: B(NMAX)
!                                    ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                                           ^^^^^ definition scip-fortran . f77-fixed . `/work/`#iters.
!                                                 kind StaticField
!                                                 display_name ITERS
!                                                 signature_documentation
!                                                 > integer :: ITERS
       DO 50 I = 1, N
!         ^^ reference local 21
!            ^ definition local 22
!              kind Variable
!              display_name I
!              signature_documentation
!              > integer :: I
!              enclosing_symbol scip-fortran . f77-fixed . sweep().
!              documentation
!              > Implicitly typed.
!                   ^ reference local 19
          S = B(I)
!         ^ definition local 23
!           kind Variable
!           display_name S
!           signature_documentation
!           > real :: S
!           enclosing_symbol scip-fortran . f77-fixed . sweep().
!           documentation
!           > Implicitly typed.
!             ^ reference scip-fortran . f77-fixed . `/work/`#b.
!               ^ reference local 22
          DO 60 J = 1, N
!            ^^ reference local 24
!               ^ definition local 25
!                 kind Variable
!                 display_name J
!                 signature_documentation
!                 > integer :: J
!                 enclosing_symbol scip-fortran . f77-fixed . sweep().
!                 documentation
!                 > Implicitly typed.
!                      ^ reference local 19
             IF (J .NE. I) S = S - A(I,J)*X(J)
!                ^ reference local 25
!                       ^ reference local 22
!                          ^ reference local 23
!                              ^ reference local 23
!                                  ^ reference scip-fortran . f77-fixed . `/work/`#a.
!                                    ^ reference local 22
!                                      ^ reference local 25
!                                         ^ reference local 20
!                                           ^ reference local 25
    60    CONTINUE
!   ^^ definition local 24
!      display_name 60
!      signature_documentation
!      > 60 CONTINUE
!      enclosing_symbol scip-fortran . f77-fixed . sweep().
          X(I) = S/A(I,I)
!         ^ reference local 20
!           ^ reference local 22
!                ^ reference local 23
!                  ^ reference scip-fortran . f77-fixed . `/work/`#a.
!                    ^ reference local 22
!                      ^ reference local 22
    50 CONTINUE
!   ^^ definition local 21
!      display_name 50
!      signature_documentation
!      > 50 CONTINUE
!      enclosing_symbol scip-fortran . f77-fixed . sweep().
       ITERS = ITERS + 1
!      ^^^^^ reference scip-fortran . f77-fixed . `/work/`#iters.
!              ^^^^^ reference scip-fortran . f77-fixed . `/work/`#iters.
       END
!        ⌃ enclosing_range_end scip-fortran . f77-fixed . sweep().
 C
!      ⌄ enclosing_range_start scip-fortran . f77-fixed . resid().
       DOUBLE PRECISION FUNCTION RESID(N, X)
!                                ^^^^^ definition scip-fortran . f77-fixed . resid().
!                                      kind Function
!                                      display_name RESID
!                                      signature_documentation
!                                      > DOUBLE PRECISION FUNCTION RESID(N, X)
!                                      >   integer :: N
!                                      >   DOUBLE PRECISION :: X(N)
!                                      ^ definition local 26
!                                        kind Parameter
!                                        display_name N
!                                        signature_documentation
!                                        > integer :: N
!                                        enclosing_symbol scip-fortran . f77-fixed . resid().
!                                         ^ definition local 27
!                                           kind Parameter
!                                           display_name X
!                                           signature_documentation
!                                           > DOUBLE PRECISION :: X(N)
!                                           enclosing_symbol scip-fortran . f77-fixed . resid().
       INCLUDE 'params.inc'
       DOUBLE PRECISION X(N)
!                       ^ definition local 27
!                         kind Parameter
!                         display_name X
!                         signature_documentation
!                         > DOUBLE PRECISION :: X(N)
!                         enclosing_symbol scip-fortran . f77-fixed . resid().
!                         ^ reference local 26
       COMMON /WORK/ A(NMAX,NMAX), B(NMAX), ITERS
!              ^^^^ definition scip-fortran . f77-fixed . `/work/`#
!                   kind Struct
!                   display_name /WORK/
!                   signature_documentation
!                   > common /WORK/
!                    ^ definition scip-fortran . f77-fixed . `/work/`#a.
!                      kind StaticField
!                      display_name A
!                      signature_documentation
!                      > double precision :: A(NMAX,NMAX)
!                      ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                           ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                                  ^ definition scip-fortran . f77-fixed . `/work/`#b.
!                                    kind StaticField
!                                    display_name B
!                                    signature_documentation
!                                    > double precision :: B(NMAX)
!                                    ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                                           ^^^^^ definition scip-fortran . f77-fixed . `/work/`#iters.
!                                                 kind StaticField
!                                                 display_name ITERS
!                                                 signature_documentation
!                                                 > integer :: ITERS
       RESID = 0.0D0
!      ^^^^^ reference scip-fortran . f77-fixed . resid().
       DO 70 I = 1, N
!         ^^ reference local 28
!            ^ definition local 29
!              kind Variable
!              display_name I
!              signature_documentation
!              > integer :: I
!              enclosing_symbol scip-fortran . f77-fixed . resid().
!              documentation
!              > Implicitly typed.
!                   ^ reference local 26
          T = -B(I)
!         ^ definition local 30
!           kind Variable
!           display_name T
!           signature_documentation
!           > real :: T
!           enclosing_symbol scip-fortran . f77-fixed . resid().
!           documentation
!           > Implicitly typed.
!              ^ reference scip-fortran . f77-fixed . `/work/`#b.
!                ^ reference local 29
          DO 80 J = 1, N
!            ^^ reference local 31
!               ^ definition local 32
!                 kind Variable
!                 display_name J
!                 signature_documentation
!                 > integer :: J
!                 enclosing_symbol scip-fortran . f77-fixed . resid().
!                 documentation
!                 > Implicitly typed.
!                      ^ reference local 26
             T = T + A(I,J)*X(J)                                         SEQ00010
!            ^ reference local 30
!                ^ reference local 30
!                    ^ reference scip-fortran . f77-fixed . `/work/`#a.
!                      ^ reference local 29
!                        ^ reference local 32
!                           ^ reference local 27
!                             ^ reference local 32
    80    CONTINUE
!   ^^ definition local 31
!      display_name 80
!      signature_documentation
!      > 80 CONTINUE
!      enclosing_symbol scip-fortran . f77-fixed . resid().
          RESID = MAX(RESID, ABS(T))
!         ^^^^^ reference scip-fortran . f77-fixed . resid().
!                     ^^^^^ reference scip-fortran . f77-fixed . resid().
!                                ^ reference local 30
    70 CONTINUE
!   ^^ definition local 28
!      display_name 70
!      signature_documentation
!      > 70 CONTINUE
!      enclosing_symbol scip-fortran . f77-fixed . resid().
       END
!        ⌃ enclosing_range_end scip-fortran . f77-fixed . resid().
 C
!      ⌄ enclosing_range_start scip-fortran . f77-fixed . workinit/
       BLOCK DATA WORKINIT
!                 ^^^^^^^^ definition scip-fortran . f77-fixed . workinit/
!                          kind Namespace
!                          display_name WORKINIT
!                          signature_documentation
!                          > BLOCK DATA WORKINIT
       INCLUDE 'params.inc'
       COMMON /WORK/ A(NMAX,NMAX), B(NMAX), ITERS
!              ^^^^ definition scip-fortran . f77-fixed . `/work/`#
!                   kind Struct
!                   display_name /WORK/
!                   signature_documentation
!                   > common /WORK/
!                    ^ definition scip-fortran . f77-fixed . `/work/`#a.
!                      kind StaticField
!                      display_name A
!                      signature_documentation
!                      > double precision :: A(NMAX,NMAX)
!                      ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                           ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                                  ^ definition scip-fortran . f77-fixed . `/work/`#b.
!                                    kind StaticField
!                                    display_name B
!                                    signature_documentation
!                                    > double precision :: B(NMAX)
!                                    ^^^^ reference scip-fortran . f77-fixed . `params.inc`/nmax.
!                                           ^^^^^ definition scip-fortran . f77-fixed . `/work/`#iters.
!                                                 kind StaticField
!                                                 display_name ITERS
!                                                 signature_documentation
!                                                 > integer :: ITERS
       DATA ITERS /0/
!           ^^^^^ reference scip-fortran . f77-fixed . `/work/`#iters.
       END
!        ⌃ enclosing_range_end scip-fortran . f77-fixed . workinit/
 
