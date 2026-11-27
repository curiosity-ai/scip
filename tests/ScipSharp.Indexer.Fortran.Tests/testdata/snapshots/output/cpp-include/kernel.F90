 #include "config.h"
!⌄ enclosing_range_start scip-fortran . cpp-include . kernel/
 module kernel
!       ^^^^^^ definition scip-fortran . cpp-include . kernel/
!              kind Module
!              display_name kernel
!              signature_documentation
!              > module kernel
   implicit none
 #ifdef USE_DOUBLE
   integer, parameter :: rk = selected_real_kind(15)
!                        ^^ definition scip-fortran . cpp-include . kernel/rk.
!                           kind Constant
!                           display_name rk
!                           signature_documentation
!                           > integer, parameter :: rk = selected_real_kind(6)
 #else
   integer, parameter :: rk = selected_real_kind(6)
!                        ^^ definition scip-fortran . cpp-include . kernel/rk.
!                           kind Constant
!                           display_name rk
!                           signature_documentation
!                           > integer, parameter :: rk = selected_real_kind(6)
 #endif
   real(rk) :: scale_factor = 2.0_rk; integer :: calls = 0
!       ^^ reference scip-fortran . cpp-include . kernel/rk.
!              ^^^^^^^^^^^^ definition scip-fortran . cpp-include . kernel/scale_factor.
!                           kind Variable
!                           display_name scale_factor
!                           signature_documentation
!                           > real(rk) :: scale_factor = 2.0_rk
!                                 ^^ reference scip-fortran . cpp-include . kernel/rk.
!                                                ^^^^^ definition scip-fortran . cpp-include . kernel/calls.
!                                                      kind Variable
!                                                      display_name calls
!                                                      signature_documentation
!                                                      > integer :: calls = 0
 contains
!  ⌄ enclosing_range_start scip-fortran . cpp-include . kernel/apply().
   subroutine apply(x, &
!             ^^^^^ definition scip-fortran . cpp-include . kernel/apply().
!                   kind Function
!                   display_name apply
!                   signature_documentation
!                   > subroutine apply(x, n)
!                   >   real(rk), intent(inout) :: x(n)
!                   >   integer, intent(in) :: n
!                   ^ definition local 0
!                     kind Parameter
!                     display_name x
!                     signature_documentation
!                     > real(rk), intent(inout) :: x(n)
!                     enclosing_symbol scip-fortran . cpp-include . kernel/apply().
                    n)
!                   ^ definition local 1
!                     kind Parameter
!                     display_name n
!                     signature_documentation
!                     > integer, intent(in) :: n
!                     enclosing_symbol scip-fortran . cpp-include . kernel/apply().
     integer, intent(in) :: n
!                           ^ definition local 1
!                             kind Parameter
!                             display_name n
!                             signature_documentation
!                             > integer, intent(in) :: n
!                             enclosing_symbol scip-fortran . cpp-include . kernel/apply().
     real(rk), intent(inout) :: x(n)
!         ^^ reference scip-fortran . cpp-include . kernel/rk.
!                               ^ definition local 0
!                                 kind Parameter
!                                 display_name x
!                                 signature_documentation
!                                 > real(rk), intent(inout) :: x(n)
!                                 enclosing_symbol scip-fortran . cpp-include . kernel/apply().
!                                 ^ reference local 1
     x = x * scale_factor &
!    ^ reference local 0
!        ^ reference local 0
!            ^^^^^^^^^^^^ reference scip-fortran . cpp-include . kernel/scale_factor.
           + VECTOR_LEN
     calls = calls + 1
!    ^^^^^ reference scip-fortran . cpp-include . kernel/calls.
!            ^^^^^ reference scip-fortran . cpp-include . kernel/calls.
     print *, "applied to ", n, " elements; &
!                            ^ reference local 1
              &calls so far:", calls
!                              ^^^^^ reference scip-fortran . cpp-include . kernel/calls.
   end subroutine apply
!                 ^^^^^ reference scip-fortran . cpp-include . kernel/apply().
!                     ⌃ enclosing_range_end scip-fortran . cpp-include . kernel/apply().
 end module kernel
!           ^^^^^^ reference scip-fortran . cpp-include . kernel/
!                ⌃ enclosing_range_end scip-fortran . cpp-include . kernel/
 
