 C     Problem size and tolerance shared by every routine.
       INTEGER NMAX
!              ^^^^ definition scip-fortran . f77-fixed . `params.inc`/nmax.
!                   kind Constant
!                   display_name NMAX
!                   signature_documentation
!                   > INTEGER, parameter :: NMAX = 8
       DOUBLE PRECISION TOL
!                       ^^^ definition scip-fortran . f77-fixed . `params.inc`/tol.
!                           kind Constant
!                           display_name TOL
!                           signature_documentation
!                           > DOUBLE PRECISION, parameter :: TOL = 1.0D-10
       PARAMETER (NMAX = 8, TOL = 1.0D-10)
!                 ^^^^ definition scip-fortran . f77-fixed . `params.inc`/nmax.
!                      kind Constant
!                      display_name NMAX
!                      signature_documentation
!                      > INTEGER, parameter :: NMAX = 8
!                           ^^^ definition scip-fortran . f77-fixed . `params.inc`/tol.
!                               kind Constant
!                               display_name TOL
!                               signature_documentation
!                               > DOUBLE PRECISION, parameter :: TOL = 1.0D-10
 
