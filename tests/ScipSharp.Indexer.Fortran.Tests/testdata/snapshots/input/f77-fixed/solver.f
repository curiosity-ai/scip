*> \brief SOLVE runs a toy Gauss-Seidel sweep.
*>
*> \verbatim
*>  Uses the shared /WORK/ common block.
*> \endverbatim
C     ==================================================================
      PROGRAM SOLVE
      IMPLICIT DOUBLE PRECISION (A-H,O-Z)
      INCLUDE 'params.inc'
      DIMENSION X(NMAX)
      COMMON /WORK/ A(NMAX,NMAX), B(NMAX), ITERS
      EXTERNAL RESID
      DOUBLE PRECISION RESID
      ABS2(Z) = Z*Z
C
      CALL SETUP(N)
      DO 10 I = 1, N
         X(I) = 0.0D0
   10 CONTINUE
      DO 30 K = 1, 100
         CALL SWEEP(N, X)
         R = RESID(N, X)
         IF (ABS2(R) .LT. TOL*TOL) GO TO 40
   30 CONTINUE
   40 WRITE (6, 900) K, R
  900 FORMAT (' ITERATIONS ', I5, ' RESIDUAL ', 1PE12.4)
      STOP
      END
C
      SUBROUTINE SETUP(N)
      INCLUDE 'params.inc'
      COMMON /WORK/ A(NMAX,NMAX), B(NMAX), ITERS
      N = NMAX
      DO 20 I = 1, N
         DO 20 J = 1, N
            A(I,J) = 1.0D0/DBLE(I+J-1)
            IF (I .EQ. J) A(I,J) = A(I,J) + DBLE(N)
   20 CONTINUE
      CALLFILLB(N,
     $          B)
      RETURN
      END
C
      SUBROUTINE FILLB(N, B)
      DOUBLE PRECISION B(*)
      DO 5 I = 1, N
    5 B(I) = 1.0D0
      RETURN
      ENTRY FILLB0(N, B)
      B(1) = 0.0D0
      END
C
      SUBROUTINE SWEEP(N, X)
      INCLUDE 'params.inc'
      DOUBLE PRECISION X(N)
      COMMON /WORK/ A(NMAX,NMAX), B(NMAX), ITERS
      DO 50 I = 1, N
         S = B(I)
         DO 60 J = 1, N
            IF (J .NE. I) S = S - A(I,J)*X(J)
   60    CONTINUE
         X(I) = S/A(I,I)
   50 CONTINUE
      ITERS = ITERS + 1
      END
C
      DOUBLE PRECISION FUNCTION RESID(N, X)
      INCLUDE 'params.inc'
      DOUBLE PRECISION X(N)
      COMMON /WORK/ A(NMAX,NMAX), B(NMAX), ITERS
      RESID = 0.0D0
      DO 70 I = 1, N
         T = -B(I)
         DO 80 J = 1, N
            T = T + A(I,J)*X(J)                                         SEQ00010
   80    CONTINUE
         RESID = MAX(RESID, ABS(T))
   70 CONTINUE
      END
C
      BLOCK DATA WORKINIT
      INCLUDE 'params.inc'
      COMMON /WORK/ A(NMAX,NMAX), B(NMAX), ITERS
      DATA ITERS /0/
      END
