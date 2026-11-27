 !> Basic geometry kinds and helpers.
!⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 geometry_kinds/
 module geometry_kinds
!       ^^^^^^^^^^^^^^ definition scip-fortran fpm geometry 1.2.0 geometry_kinds/
!                      kind Module
!                      display_name geometry_kinds
!                      signature_documentation
!                      > module geometry_kinds
!                      documentation
!                      > Basic geometry kinds and helpers.
   implicit none
   private
   integer, parameter, public :: dp = kind(1.0d0)  !! double precision kind
!                                ^^ definition scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                                   kind Constant
!                                   display_name dp
!                                   signature_documentation
!                                   > integer, parameter, public :: dp = kind(1.0d0)
!                                   documentation
!                                   > double precision kind
   real(dp), parameter, public :: pi = 3.141592653589793_dp
!       ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                                 ^^ definition scip-fortran fpm geometry 1.2.0 geometry_kinds/pi.
!                                    kind Constant
!                                    display_name pi
!                                    signature_documentation
!                                    > real(dp), parameter, public :: pi = 3.141592653589793_dp
!                                                        ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
   public :: deg2rad
!            ^^^^^^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
 contains
   !> Convert degrees to radians.
!  ⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
   pure elemental function deg2rad(deg) result(rad)
!                          ^^^^^^^ definition scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
!                                  kind Function
!                                  display_name deg2rad
!                                  signature_documentation
!                                  > pure elemental function deg2rad(deg) result(rad)
!                                  >   real(dp), intent(in) :: deg
!                                  >   real(dp) :: rad
!                                  documentation
!                                  > Convert degrees to radians.
!                                  ^^^ definition local 0
!                                      kind Parameter
!                                      display_name deg
!                                      signature_documentation
!                                      > real(dp), intent(in) :: deg
!                                      enclosing_symbol scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
!                                      documentation
!                                      > angle in degrees
!                                              ^^^ definition local 1
!                                                  kind Variable
!                                                  display_name rad
!                                                  signature_documentation
!                                                  > real(dp) :: rad
!                                                  enclosing_symbol scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
     real(dp), intent(in) :: deg  !! angle in degrees
!         ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                            ^^^ definition local 0
!                                kind Parameter
!                                display_name deg
!                                signature_documentation
!                                > real(dp), intent(in) :: deg
!                                enclosing_symbol scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
!                                documentation
!                                > angle in degrees
     real(dp) :: rad
!         ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                ^^^ definition local 1
!                    kind Variable
!                    display_name rad
!                    signature_documentation
!                    > real(dp) :: rad
!                    enclosing_symbol scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
     rad = deg * pi / 180.0_dp
!    ^^^ reference local 1
!          ^^^ reference local 0
!                ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/pi.
!                           ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
   end function deg2rad
!               ^^^^^^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
!                     ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
 end module geometry_kinds
!           ^^^^^^^^^^^^^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/
!                        ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 geometry_kinds/
 
!⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 vectors/
 module vectors
!       ^^^^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/
!               kind Module
!               display_name vectors
!               signature_documentation
!               > module vectors
   use geometry_kinds, only: dp, to_rad => deg2rad
!      ^^^^^^^^^^^^^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/
!                            ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                                ^^^^^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
!                                          ^^^^^^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
   implicit none
 
   !> A 3D vector.
!  ⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 vectors/vec3#
   type :: vec3
!          ^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/vec3#
!               kind Struct
!               display_name vec3
!               signature_documentation
!               > type :: vec3
!               documentation
!               > A 3D vector.
     real(dp) :: x = 0, y = 0, z = 0
!         ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                ^ definition scip-fortran fpm geometry 1.2.0 vectors/vec3#x.
!                  kind Field
!                  display_name x
!                  signature_documentation
!                  > real(dp) :: x = 0
!                       ^ definition scip-fortran fpm geometry 1.2.0 vectors/vec3#y.
!                         kind Field
!                         display_name y
!                         signature_documentation
!                         > real(dp) :: y = 0
!                              ^ definition scip-fortran fpm geometry 1.2.0 vectors/vec3#z.
!                                kind Field
!                                display_name z
!                                signature_documentation
!                                > real(dp) :: z = 0
   end type vec3
!           ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#
!              ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 vectors/vec3#
 
   interface operator(+)
!                     ^ definition scip-fortran fpm geometry 1.2.0 vectors/`operator(+)`().
!                       kind Operator
!                       display_name operator(+)
!                       signature_documentation
!                       > interface operator(+)
!                       >   procedure :: vec_add
     module procedure vec_add
!                     ^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec_add().
   end interface
 
   interface operator(.dot.)
!                     ^^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/`operator(.dot.)`().
!                           kind Operator
!                           display_name operator(.dot.)
!                           signature_documentation
!                           > interface operator(.dot.)
!                           >   procedure :: vec_dot
     module procedure vec_dot
!                     ^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec_dot().
   end interface
 
   !> Overloaded norm.
   interface norm
!            ^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/norm().
!                 kind Interface
!                 display_name norm
!                 signature_documentation
!                 > interface norm
!                 >   procedure :: norm_vec
!                 >   procedure :: norm_array
!                 documentation
!                 > Overloaded norm.
     module procedure norm_vec, norm_array
!                     ^^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/norm_vec().
!                               ^^^^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/norm_array().
   end interface norm
!                ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/norm().
 
   interface assignment(=)
!                       ^ definition scip-fortran fpm geometry 1.2.0 vectors/`assignment(=)`().
!                         kind Operator
!                         display_name assignment(=)
!                         signature_documentation
!                         > interface assignment(=)
!                         >   procedure :: vec_from_scalar
     module procedure vec_from_scalar
!                     ^^^^^^^^^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec_from_scalar().
   end interface
 
   real(dp), save :: origin_offset
!       ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                    ^^^^^^^^^^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/origin_offset.
!                                  kind Variable
!                                  display_name origin_offset
!                                  signature_documentation
!                                  > real(dp), save :: origin_offset
   namelist /vecparams/ origin_offset
!            ^^^^^^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/vecparams.
!                      kind Variable
!                      display_name vecparams
!                      signature_documentation
!                      > namelist /vecparams/ origin_offset
!                       ^^^^^^^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/origin_offset.
 
 contains
 
!  ⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 vectors/vec_add().
   pure function vec_add(a, b) result(c)
!                ^^^^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/vec_add().
!                        kind Function
!                        display_name vec_add
!                        signature_documentation
!                        > pure function vec_add(a, b) result(c)
!                        >   type(vec3), intent(in) :: a
!                        >   type(vec3), intent(in) :: b
!                        >   type(vec3) :: c
!                        relationship scip-fortran fpm geometry 1.2.0 vectors/`operator(+)`(). implementation
!                        ^ definition local 2
!                          kind Parameter
!                          display_name a
!                          signature_documentation
!                          > type(vec3), intent(in) :: a
!                          enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_add().
!                           ^ definition local 3
!                             kind Parameter
!                             display_name b
!                             signature_documentation
!                             > type(vec3), intent(in) :: b
!                             enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_add().
!                                     ^ definition local 4
!                                       kind Variable
!                                       display_name c
!                                       signature_documentation
!                                       > type(vec3) :: c
!                                       enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_add().
     type(vec3), intent(in) :: a, b
!         ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#
!                              ^ definition local 2
!                                kind Parameter
!                                display_name a
!                                signature_documentation
!                                > type(vec3), intent(in) :: a
!                                enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_add().
!                                 ^ definition local 3
!                                   kind Parameter
!                                   display_name b
!                                   signature_documentation
!                                   > type(vec3), intent(in) :: b
!                                   enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_add().
     type(vec3) :: c
!         ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#
!                  ^ definition local 4
!                    kind Variable
!                    display_name c
!                    signature_documentation
!                    > type(vec3) :: c
!                    enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_add().
     c%x = a%x + b%x
!    ^ reference local 4
!      ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#x.
!          ^ reference local 2
!            ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#x.
!                ^ reference local 3
!                  ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#x.
     c%y = a%y + b%y
!    ^ reference local 4
!      ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#y.
!          ^ reference local 2
!            ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#y.
!                ^ reference local 3
!                  ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#y.
     c%z = a%z + b%z
!    ^ reference local 4
!      ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#z.
!          ^ reference local 2
!            ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#z.
!                ^ reference local 3
!                  ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#z.
   end function
!             ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 vectors/vec_add().
 
!  ⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 vectors/vec_dot().
   pure real(dp) function vec_dot(a, b)
!            ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                         ^^^^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/vec_dot().
!                                 kind Function
!                                 display_name vec_dot
!                                 signature_documentation
!                                 > pure real(dp) function vec_dot(a, b)
!                                 >   type(vec3), intent(in) :: a
!                                 >   type(vec3), intent(in) :: b
!                                 relationship scip-fortran fpm geometry 1.2.0 vectors/`operator(.dot.)`(). implementation
!                                 ^ definition local 5
!                                   kind Parameter
!                                   display_name a
!                                   signature_documentation
!                                   > type(vec3), intent(in) :: a
!                                   enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_dot().
!                                    ^ definition local 6
!                                      kind Parameter
!                                      display_name b
!                                      signature_documentation
!                                      > type(vec3), intent(in) :: b
!                                      enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_dot().
     type(vec3), intent(in) :: a, b
!         ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#
!                              ^ definition local 5
!                                kind Parameter
!                                display_name a
!                                signature_documentation
!                                > type(vec3), intent(in) :: a
!                                enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_dot().
!                                 ^ definition local 6
!                                   kind Parameter
!                                   display_name b
!                                   signature_documentation
!                                   > type(vec3), intent(in) :: b
!                                   enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_dot().
     vec_dot = a%x*b%x + a%y*b%y + a%z*b%z
!    ^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec_dot().
!              ^ reference local 5
!                ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#x.
!                  ^ reference local 6
!                    ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#x.
!                        ^ reference local 5
!                          ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#y.
!                            ^ reference local 6
!                              ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#y.
!                                  ^ reference local 5
!                                    ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#z.
!                                      ^ reference local 6
!                                        ^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#z.
   end function vec_dot
!               ^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec_dot().
!                     ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 vectors/vec_dot().
 
!  ⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 vectors/norm_vec().
   real(dp) function norm_vec(v)
!       ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                    ^^^^^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/norm_vec().
!                             kind Function
!                             display_name norm_vec
!                             signature_documentation
!                             > real(dp) function norm_vec(v)
!                             >   type(vec3), intent(in) :: v
!                             relationship scip-fortran fpm geometry 1.2.0 vectors/norm(). implementation
!                             ^ definition local 7
!                               kind Parameter
!                               display_name v
!                               signature_documentation
!                               > type(vec3), intent(in) :: v
!                               enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/norm_vec().
     type(vec3), intent(in) :: v
!         ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#
!                              ^ definition local 7
!                                kind Parameter
!                                display_name v
!                                signature_documentation
!                                > type(vec3), intent(in) :: v
!                                enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/norm_vec().
     norm_vec = sqrt(v .dot. v)
!    ^^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/norm_vec().
!                    ^ reference local 7
!                      ^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/`operator(.dot.)`().
!                            ^ reference local 7
   end function
!             ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 vectors/norm_vec().
 
!  ⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 vectors/norm_array().
   real(dp) function norm_array(v)
!       ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                    ^^^^^^^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/norm_array().
!                               kind Function
!                               display_name norm_array
!                               signature_documentation
!                               > real(dp) function norm_array(v)
!                               >   real(dp), intent(in) :: v(:)
!                               relationship scip-fortran fpm geometry 1.2.0 vectors/norm(). implementation
!                               ^ definition local 8
!                                 kind Parameter
!                                 display_name v
!                                 signature_documentation
!                                 > real(dp), intent(in) :: v(:)
!                                 enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/norm_array().
     real(dp), intent(in) :: v(:)
!         ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                            ^ definition local 8
!                              kind Parameter
!                              display_name v
!                              signature_documentation
!                              > real(dp), intent(in) :: v(:)
!                              enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/norm_array().
     norm_array = sqrt(sum(v**2))
!    ^^^^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/norm_array().
!                          ^ reference local 8
   end function
!             ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 vectors/norm_array().
 
!  ⌄ enclosing_range_start scip-fortran fpm geometry 1.2.0 vectors/vec_from_scalar().
   subroutine vec_from_scalar(v, s)
!             ^^^^^^^^^^^^^^^ definition scip-fortran fpm geometry 1.2.0 vectors/vec_from_scalar().
!                             kind Function
!                             display_name vec_from_scalar
!                             signature_documentation
!                             > subroutine vec_from_scalar(v, s)
!                             >   type(vec3), intent(out) :: v
!                             >   real(dp), intent(in) :: s
!                             relationship scip-fortran fpm geometry 1.2.0 vectors/`assignment(=)`(). implementation
!                             ^ definition local 9
!                               kind Parameter
!                               display_name v
!                               signature_documentation
!                               > type(vec3), intent(out) :: v
!                               enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_from_scalar().
!                                ^ definition local 10
!                                  kind Parameter
!                                  display_name s
!                                  signature_documentation
!                                  > real(dp), intent(in) :: s
!                                  enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_from_scalar().
     type(vec3), intent(out) :: v
!         ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#
!                               ^ definition local 9
!                                 kind Parameter
!                                 display_name v
!                                 signature_documentation
!                                 > type(vec3), intent(out) :: v
!                                 enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_from_scalar().
     real(dp), intent(in) :: s
!         ^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/dp.
!                            ^ definition local 10
!                              kind Parameter
!                              display_name s
!                              signature_documentation
!                              > real(dp), intent(in) :: s
!                              enclosing_symbol scip-fortran fpm geometry 1.2.0 vectors/vec_from_scalar().
     v = vec3(s, s, to_rad(s))
!    ^ reference local 9
!        ^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/vec3#
!             ^ reference local 10
!                ^ reference local 10
!                   ^^^^^^ reference scip-fortran fpm geometry 1.2.0 geometry_kinds/deg2rad().
!                          ^ reference local 10
   end subroutine
!               ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 vectors/vec_from_scalar().
 
 end module vectors
!           ^^^^^^^ reference scip-fortran fpm geometry 1.2.0 vectors/
!                 ⌃ enclosing_range_end scip-fortran fpm geometry 1.2.0 vectors/
 
