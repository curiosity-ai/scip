!⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 demo/
 program demo
!        ^^^^ definition scip-fortran fpm geometry 1.2.0 demo/
!             kind Namespace
!             display_name demo
!             signature_documentation
!             > program demo
   use vectors
!      ^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/
   use geometry_kinds, only: dp, pi
!      ^^^^^^^^^^^^^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/
!                            ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                                ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/pi.
   implicit none
   type(vec3) :: a, b, c
!       ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#
!                ^ definition local 0
!                  kind Variable
!                  display_name a
!                  signature_documentation
!                  > type(vec3) :: a
!                  enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/
!                   ^ definition local 1
!                     kind Variable
!                     display_name b
!                     signature_documentation
!                     > type(vec3) :: b
!                     enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/
!                      ^ definition local 2
!                        kind Variable
!                        display_name c
!                        signature_documentation
!                        > type(vec3) :: c
!                        enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/
   real(dp) :: n
!       ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!              ^ definition local 3
!                kind Variable
!                display_name n
!                signature_documentation
!                > real(dp) :: n
!                enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/
   integer :: i, ios
!             ^ definition local 4
!               kind Variable
!               display_name i
!               signature_documentation
!               > integer :: i
!               enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/
!                ^^^ definition local 5
!                    kind Variable
!                    display_name ios
!                    signature_documentation
!                    > integer :: ios
!                    enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/
   real(dp), dimension(3) :: arr = [1.0_dp, 2.0_dp, 3.0_dp]
!       ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                            ^^^ definition local 6
!                                kind Variable
!                                display_name arr
!                                signature_documentation
!                                > real(dp) :: arr(3) = [1.0_dp, 2.0_dp, 3.0_dp]
!                                enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/
!                                       ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                                               ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                                                       ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
 
   a = vec3(1.0_dp, 0.0_dp, 0.0_dp)
!  ^ reference local 0
!      ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#
!               ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                       ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                               ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
   b = 2.0_dp
!  ^ reference local 1
!          ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
   c = a + b
!  ^ reference local 2
!      ^ reference local 0
!          ^ reference local 1
   n = norm(c) + norm(arr) + (a .dot. b)
!  ^ reference local 3
!      ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/norm().
!           ^ reference local 2
!                ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/norm().
!                     ^^^ reference local 6
!                             ^ reference local 0
!                               ^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/`operator(.dot.)`().
!                                     ^ reference local 1
   outer: do i = 1, 3
!  ^^^^^ definition local 7
!        display_name outer
!        enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/
!            ^ reference local 4
     if (arr(i) > pi) exit outer
!        ^^^ reference local 6
!            ^ reference local 4
!                 ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/pi.
!                          ^^^^^ reference local 7
     call report(i, arr(i))
!         ^^^^^^ reference scip-fortran fpm geometry 1.2.0 demo/report().
!                ^ reference local 4
!                   ^^^ reference local 6
!                       ^ reference local 4
   end do outer
!         ^^^^^ reference local 7
   open (unit=10, file='params.nml', status='old', iostat=ios)
!                                                         ^^^ reference local 5
   read (10, nml=vecparams, iostat=ios)
!                ^^^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vecparams.
!                                  ^^^ reference local 5
   write (*, 100) n
!            ^^^ reference local 8
!                 ^ reference local 3
 100 format (1x, 'norm = ', f10.4)
!^^^ definition local 8
!    display_name 100
!    signature_documentation
!    > 100 format (1x, 'norm = ', f10.4)
!    enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/
 
 contains
 
!  ⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 demo/report().
   subroutine report(k, value)
!             ^^^^^^ definition scip-fortran fpm geometry 1.2.0 demo/report().
!                    kind Function
!                    display_name report
!                    signature_documentation
!                    > subroutine report(k, value)
!                    >   integer, intent(in) :: k
!                    >   real(dp), intent(in) :: value
!                    ^ definition local 9
!                      kind Parameter
!                      display_name k
!                      signature_documentation
!                      > integer, intent(in) :: k
!                      enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/report().
!                       ^^^^^ definition local 10
!                             kind Parameter
!                             display_name value
!                             signature_documentation
!                             > real(dp), intent(in) :: value
!                             enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/report().
     integer, intent(in) :: k
!                           ^ definition local 9
!                             kind Parameter
!                             display_name k
!                             signature_documentation
!                             > integer, intent(in) :: k
!                             enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/report().
     real(dp), intent(in) :: value
!         ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                            ^^^^^ definition local 10
!                                  kind Parameter
!                                  display_name value
!                                  signature_documentation
!                                  > real(dp), intent(in) :: value
!                                  enclosing_symbol scip-fortran fpm geometry 1.2.0 demo/report().
     print '(i3, 1x, f8.3)', k, value + c%x
!                            ^ reference local 9
!                               ^^^^^ reference local 10
!                                       ^ reference local 2
!                                         ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#x.
   end subroutine report
!                 ^^^^^^ reference scip-fortran fpm geometry 1.2.0 demo/report().
!                      ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 demo/report().
 
 end program demo
!            ^^^^ reference scip-fortran fpm geometry 1.2.0 demo/
!               ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 demo/
 
