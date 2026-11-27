 !> Object-oriented shapes (Fortran 2003).
!⌄ enclosing_range_start scip-fortran . f2003-oop . shapes/
 module shapes
!       ^^^^^^ definition scip-fortran . f2003-oop . shapes/
!              kind Module
!              display_name shapes
!              signature_documentation
!              > module shapes
!              documentation
!              > Object-oriented shapes (Fortran 2003).
   use, intrinsic :: iso_fortran_env, only: wp => real64
   implicit none
   private
   public :: shape, circle, square, area_iface, make_circle
!            ^^^^^ reference scip-fortran . f2003-oop . shapes/shape#
!                   ^^^^^^ reference scip-fortran . f2003-oop . shapes/circle#
!                           ^^^^^^ reference scip-fortran . f2003-oop . shapes/square#
!                                   ^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/area_iface().
!                                               ^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/make_circle().
 
   !> Abstract base type.
!  ⌄ enclosing_range_start scip-fortran . f2003-oop . shapes/shape#
   type, abstract :: shape
!                    ^^^^^ definition scip-fortran . f2003-oop . shapes/shape#
!                          kind Class
!                          display_name shape
!                          signature_documentation
!                          > type, abstract :: shape
!                          documentation
!                          > Abstract base type.
     character(len=:), allocatable :: name
!                                     ^^^^ definition scip-fortran . f2003-oop . shapes/shape#name.
!                                          kind Field
!                                          display_name name
!                                          signature_documentation
!                                          > character(len=:), allocatable :: name
   contains
     procedure(area_iface), deferred :: area
!              ^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/area_iface().
!                                       ^^^^ definition scip-fortran . f2003-oop . shapes/shape#area().
!                                            kind AbstractMethod
!                                            display_name area
!                                            signature_documentation
!                                            > procedure(area_iface), deferred :: area
     procedure :: describe => shape_describe
!                 ^^^^^^^^ definition scip-fortran . f2003-oop . shapes/shape#describe().
!                          kind Method
!                          display_name describe
!                          signature_documentation
!                          > procedure :: describe => shape_describe
!                          relationship scip-fortran . f2003-oop . shapes/shape#info(). implementation
!                             ^^^^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/shape_describe().
     generic :: info => describe
!               ^^^^ definition scip-fortran . f2003-oop . shapes/shape#info().
!                    kind Method
!                    display_name info
!                    signature_documentation
!                    > generic :: info => describe
!                       ^^^^^^^^ reference scip-fortran . f2003-oop . shapes/shape#describe().
   end type shape
!           ^^^^^ reference scip-fortran . f2003-oop . shapes/shape#
!               ⌃ enclosing_range_end scip-fortran . f2003-oop . shapes/shape#
 
   abstract interface
!    ⌄ enclosing_range_start scip-fortran . f2003-oop . shapes/area_iface().
     pure function area_iface(self) result(a)
!                  ^^^^^^^^^^ definition scip-fortran . f2003-oop . shapes/area_iface().
!                             kind Interface
!                             display_name area_iface
!                             signature_documentation
!                             > pure function area_iface(self) result(a)
!                             >   class(shape), intent(in) :: self
!                             >   real(wp) :: a
!                             ^^^^ definition local 0
!                                  kind Parameter
!                                  display_name self
!                                  signature_documentation
!                                  > class(shape), intent(in) :: self
!                                  enclosing_symbol scip-fortran . f2003-oop . shapes/area_iface().
!                                          ^ definition local 1
!                                            kind Variable
!                                            display_name a
!                                            signature_documentation
!                                            > real(wp) :: a
!                                            enclosing_symbol scip-fortran . f2003-oop . shapes/area_iface().
       import :: shape, wp
!                ^^^^^ reference scip-fortran . f2003-oop . shapes/shape#
       class(shape), intent(in) :: self
!            ^^^^^ reference scip-fortran . f2003-oop . shapes/shape#
!                                  ^^^^ definition local 0
!                                       kind Parameter
!                                       display_name self
!                                       signature_documentation
!                                       > class(shape), intent(in) :: self
!                                       enclosing_symbol scip-fortran . f2003-oop . shapes/area_iface().
       real(wp) :: a
!                  ^ definition local 1
!                    kind Variable
!                    display_name a
!                    signature_documentation
!                    > real(wp) :: a
!                    enclosing_symbol scip-fortran . f2003-oop . shapes/area_iface().
     end function area_iface
!                 ^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/area_iface().
!                          ⌃ enclosing_range_end scip-fortran . f2003-oop . shapes/area_iface().
   end interface
 
!  ⌄ enclosing_range_start scip-fortran . f2003-oop . shapes/circle#
   type, extends(shape) :: circle
!                ^^^^^ reference scip-fortran . f2003-oop . shapes/shape#
!                          ^^^^^^ definition scip-fortran . f2003-oop . shapes/circle#
!                                 kind Class
!                                 display_name circle
!                                 signature_documentation
!                                 > type, extends(shape) :: circle
!                                 relationship scip-fortran . f2003-oop . shapes/shape# implementation
     real(wp) :: radius = 1.0_wp
!                ^^^^^^ definition scip-fortran . f2003-oop . shapes/circle#radius.
!                       kind Field
!                       display_name radius
!                       signature_documentation
!                       > real(wp) :: radius = 1.0_wp
   contains
     procedure :: area => circle_area
!                 ^^^^ definition scip-fortran . f2003-oop . shapes/circle#area().
!                      kind Method
!                      display_name area
!                      signature_documentation
!                      > procedure :: area => circle_area
!                      relationship scip-fortran . f2003-oop . shapes/shape#area(). implementation
!                         ^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/circle_area().
   end type circle
!           ^^^^^^ reference scip-fortran . f2003-oop . shapes/circle#
!                ⌃ enclosing_range_end scip-fortran . f2003-oop . shapes/circle#
 
!  ⌄ enclosing_range_start scip-fortran . f2003-oop . shapes/square#
   type, extends(shape) :: square
!                ^^^^^ reference scip-fortran . f2003-oop . shapes/shape#
!                          ^^^^^^ definition scip-fortran . f2003-oop . shapes/square#
!                                 kind Class
!                                 display_name square
!                                 signature_documentation
!                                 > type, extends(shape) :: square
!                                 relationship scip-fortran . f2003-oop . shapes/shape# implementation
     real(wp) :: side
!                ^^^^ definition scip-fortran . f2003-oop . shapes/square#side.
!                     kind Field
!                     display_name side
!                     signature_documentation
!                     > real(wp) :: side
   contains
     procedure :: area => square_area
!                 ^^^^ definition scip-fortran . f2003-oop . shapes/square#area().
!                      kind Method
!                      display_name area
!                      signature_documentation
!                      > procedure :: area => square_area
!                      relationship scip-fortran . f2003-oop . shapes/shape#area(). implementation
!                         ^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/square_area().
     procedure :: describe => square_describe
!                 ^^^^^^^^ definition scip-fortran . f2003-oop . shapes/square#describe().
!                          kind Method
!                          display_name describe
!                          signature_documentation
!                          > procedure :: describe => square_describe
!                          relationship scip-fortran . f2003-oop . shapes/shape#describe(). implementation
!                             ^^^^^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/square_describe().
   end type square
!           ^^^^^^ reference scip-fortran . f2003-oop . shapes/square#
!                ⌃ enclosing_range_end scip-fortran . f2003-oop . shapes/square#
 
   !> Callback type for visiting shapes.
   procedure(area_iface), pointer :: measure => null()
!            ^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/area_iface().
!                                    ^^^^^^^ definition scip-fortran . f2003-oop . shapes/measure.
!                                            kind Variable
!                                            display_name measure
!                                            signature_documentation
!                                            > procedure(area_iface), pointer :: measure
!                                            documentation
!                                            > Callback type for visiting shapes.
 
 contains
 
!  ⌄ enclosing_range_start scip-fortran . f2003-oop . shapes/circle_area().
   pure function circle_area(self) result(a)
!                ^^^^^^^^^^^ definition scip-fortran . f2003-oop . shapes/circle_area().
!                            kind Function
!                            display_name circle_area
!                            signature_documentation
!                            > pure function circle_area(self) result(a)
!                            >   class(circle), intent(in) :: self
!                            >   real(wp) :: a
!                            relationship scip-fortran . f2003-oop . shapes/circle#area(). implementation
!                            ^^^^ definition local 2
!                                 kind Parameter
!                                 display_name self
!                                 signature_documentation
!                                 > class(circle), intent(in) :: self
!                                 enclosing_symbol scip-fortran . f2003-oop . shapes/circle_area().
!                                         ^ definition local 3
!                                           kind Variable
!                                           display_name a
!                                           signature_documentation
!                                           > real(wp) :: a
!                                           enclosing_symbol scip-fortran . f2003-oop . shapes/circle_area().
     class(circle), intent(in) :: self
!          ^^^^^^ reference scip-fortran . f2003-oop . shapes/circle#
!                                 ^^^^ definition local 2
!                                      kind Parameter
!                                      display_name self
!                                      signature_documentation
!                                      > class(circle), intent(in) :: self
!                                      enclosing_symbol scip-fortran . f2003-oop . shapes/circle_area().
     real(wp) :: a
!                ^ definition local 3
!                  kind Variable
!                  display_name a
!                  signature_documentation
!                  > real(wp) :: a
!                  enclosing_symbol scip-fortran . f2003-oop . shapes/circle_area().
     a = 3.14159_wp * self%radius**2
!    ^ reference local 3
!                     ^^^^ reference local 2
!                          ^^^^^^ reference scip-fortran . f2003-oop . shapes/circle#radius.
   end function circle_area
!               ^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/circle_area().
!                         ⌃ enclosing_range_end scip-fortran . f2003-oop . shapes/circle_area().
 
!  ⌄ enclosing_range_start scip-fortran . f2003-oop . shapes/square_area().
   pure function square_area(self) result(a)
!                ^^^^^^^^^^^ definition scip-fortran . f2003-oop . shapes/square_area().
!                            kind Function
!                            display_name square_area
!                            signature_documentation
!                            > pure function square_area(self) result(a)
!                            >   class(square), intent(in) :: self
!                            >   real(wp) :: a
!                            relationship scip-fortran . f2003-oop . shapes/square#area(). implementation
!                            ^^^^ definition local 4
!                                 kind Parameter
!                                 display_name self
!                                 signature_documentation
!                                 > class(square), intent(in) :: self
!                                 enclosing_symbol scip-fortran . f2003-oop . shapes/square_area().
!                                         ^ definition local 5
!                                           kind Variable
!                                           display_name a
!                                           signature_documentation
!                                           > real(wp) :: a
!                                           enclosing_symbol scip-fortran . f2003-oop . shapes/square_area().
     class(square), intent(in) :: self
!          ^^^^^^ reference scip-fortran . f2003-oop . shapes/square#
!                                 ^^^^ definition local 4
!                                      kind Parameter
!                                      display_name self
!                                      signature_documentation
!                                      > class(square), intent(in) :: self
!                                      enclosing_symbol scip-fortran . f2003-oop . shapes/square_area().
     real(wp) :: a
!                ^ definition local 5
!                  kind Variable
!                  display_name a
!                  signature_documentation
!                  > real(wp) :: a
!                  enclosing_symbol scip-fortran . f2003-oop . shapes/square_area().
     a = self%side**2
!    ^ reference local 5
!        ^^^^ reference local 4
!             ^^^^ reference scip-fortran . f2003-oop . shapes/square#side.
   end function square_area
!               ^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/square_area().
!                         ⌃ enclosing_range_end scip-fortran . f2003-oop . shapes/square_area().
 
!  ⌄ enclosing_range_start scip-fortran . f2003-oop . shapes/shape_describe().
   subroutine shape_describe(self)
!             ^^^^^^^^^^^^^^ definition scip-fortran . f2003-oop . shapes/shape_describe().
!                            kind Function
!                            display_name shape_describe
!                            signature_documentation
!                            > subroutine shape_describe(self)
!                            >   class(shape), intent(in) :: self
!                            relationship scip-fortran . f2003-oop . shapes/shape#describe(). implementation
!                            ^^^^ definition local 6
!                                 kind Parameter
!                                 display_name self
!                                 signature_documentation
!                                 > class(shape), intent(in) :: self
!                                 enclosing_symbol scip-fortran . f2003-oop . shapes/shape_describe().
     class(shape), intent(in) :: self
!          ^^^^^ reference scip-fortran . f2003-oop . shapes/shape#
!                                ^^^^ definition local 6
!                                     kind Parameter
!                                     display_name self
!                                     signature_documentation
!                                     > class(shape), intent(in) :: self
!                                     enclosing_symbol scip-fortran . f2003-oop . shapes/shape_describe().
     print *, self%name, self%area()
!             ^^^^ reference local 6
!                  ^^^^ reference scip-fortran . f2003-oop . shapes/shape#name.
!                        ^^^^ reference local 6
!                             ^^^^ reference scip-fortran . f2003-oop . shapes/shape#area().
   end subroutine shape_describe
!                 ^^^^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/shape_describe().
!                              ⌃ enclosing_range_end scip-fortran . f2003-oop . shapes/shape_describe().
 
!  ⌄ enclosing_range_start scip-fortran . f2003-oop . shapes/square_describe().
   subroutine square_describe(self)
!             ^^^^^^^^^^^^^^^ definition scip-fortran . f2003-oop . shapes/square_describe().
!                             kind Function
!                             display_name square_describe
!                             signature_documentation
!                             > subroutine square_describe(self)
!                             >   class(square), intent(in) :: self
!                             relationship scip-fortran . f2003-oop . shapes/square#describe(). implementation
!                             ^^^^ definition local 7
!                                  kind Parameter
!                                  display_name self
!                                  signature_documentation
!                                  > class(square), intent(in) :: self
!                                  enclosing_symbol scip-fortran . f2003-oop . shapes/square_describe().
     class(square), intent(in) :: self
!          ^^^^^^ reference scip-fortran . f2003-oop . shapes/square#
!                                 ^^^^ definition local 7
!                                      kind Parameter
!                                      display_name self
!                                      signature_documentation
!                                      > class(square), intent(in) :: self
!                                      enclosing_symbol scip-fortran . f2003-oop . shapes/square_describe().
     print *, 'square of side', self%side
!                               ^^^^ reference local 7
!                                    ^^^^ reference scip-fortran . f2003-oop . shapes/square#side.
   end subroutine square_describe
!                 ^^^^^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/square_describe().
!                               ⌃ enclosing_range_end scip-fortran . f2003-oop . shapes/square_describe().
 
!  ⌄ enclosing_range_start scip-fortran . f2003-oop . shapes/make_circle().
   function make_circle(r) result(c)
!           ^^^^^^^^^^^ definition scip-fortran . f2003-oop . shapes/make_circle().
!                       kind Function
!                       display_name make_circle
!                       signature_documentation
!                       > function make_circle(r) result(c)
!                       >   real(wp), intent(in) :: r
!                       >   type(circle) :: c
!                       ^ definition local 8
!                         kind Parameter
!                         display_name r
!                         signature_documentation
!                         > real(wp), intent(in) :: r
!                         enclosing_symbol scip-fortran . f2003-oop . shapes/make_circle().
!                                 ^ definition local 9
!                                   kind Variable
!                                   display_name c
!                                   signature_documentation
!                                   > type(circle) :: c
!                                   enclosing_symbol scip-fortran . f2003-oop . shapes/make_circle().
     real(wp), intent(in) :: r
!                            ^ definition local 8
!                              kind Parameter
!                              display_name r
!                              signature_documentation
!                              > real(wp), intent(in) :: r
!                              enclosing_symbol scip-fortran . f2003-oop . shapes/make_circle().
     type(circle) :: c
!         ^^^^^^ reference scip-fortran . f2003-oop . shapes/circle#
!                    ^ definition local 9
!                      kind Variable
!                      display_name c
!                      signature_documentation
!                      > type(circle) :: c
!                      enclosing_symbol scip-fortran . f2003-oop . shapes/make_circle().
     c%radius = r
!    ^ reference local 9
!      ^^^^^^ reference scip-fortran . f2003-oop . shapes/circle#radius.
!               ^ reference local 8
     c%name = 'circle'
!    ^ reference local 9
!      ^^^^ reference scip-fortran . f2003-oop . shapes/shape#name.
   end function make_circle
!               ^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/make_circle().
!                         ⌃ enclosing_range_end scip-fortran . f2003-oop . shapes/make_circle().
 
 end module shapes
!           ^^^^^^ reference scip-fortran . f2003-oop . shapes/
!                ⌃ enclosing_range_end scip-fortran . f2003-oop . shapes/
 
!⌄ enclosing_range_start scip-fortran . f2003-oop . use_shapes/
 program use_shapes
!        ^^^^^^^^^^ definition scip-fortran . f2003-oop . use_shapes/
!                   kind Namespace
!                   display_name use_shapes
!                   signature_documentation
!                   > program use_shapes
   use shapes
!      ^^^^^^ reference scip-fortran . f2003-oop . shapes/
   implicit none
   class(shape), allocatable :: s
!        ^^^^^ reference scip-fortran . f2003-oop . shapes/shape#
!                               ^ definition local 10
!                                 kind Variable
!                                 display_name s
!                                 signature_documentation
!                                 > class(shape), allocatable :: s
!                                 enclosing_symbol scip-fortran . f2003-oop . use_shapes/
   type(circle) :: c
!       ^^^^^^ reference scip-fortran . f2003-oop . shapes/circle#
!                  ^ definition local 11
!                    kind Variable
!                    display_name c
!                    signature_documentation
!                    > type(circle) :: c
!                    enclosing_symbol scip-fortran . f2003-oop . use_shapes/
 
   c = make_circle(2.0d0)
!  ^ reference local 11
!      ^^^^^^^^^^^ reference scip-fortran . f2003-oop . shapes/make_circle().
   allocate(s, source=c)
!           ^ reference local 10
!                     ^ reference local 11
   call s%info()
!       ^ reference local 10
!         ^^^^ reference scip-fortran . f2003-oop . shapes/shape#info().
   select type (p => s)
!               ^ definition local 12
!                 kind Variable
!                 display_name p
!                 signature_documentation
!                 > real :: p
!                 enclosing_symbol scip-fortran . f2003-oop . use_shapes/
!                    ^ reference local 10
   type is (circle)
!           ^^^^^^ reference scip-fortran . f2003-oop . shapes/circle#
     print *, p%radius
!             ^ reference local 12
!               ^^^^^^ reference scip-fortran . f2003-oop . shapes/circle#radius.
   class is (square)
!            ^^^^^^ reference scip-fortran . f2003-oop . shapes/square#
     print *, p%side
!             ^ reference local 12
!               ^^^^ reference scip-fortran . f2003-oop . shapes/square#side.
   class default
     print *, 'unknown'
   end select
   associate (r => c%radius)
!             ^ definition local 13
!               kind Variable
!               display_name r
!               signature_documentation
!               > r => c%radius
!               enclosing_symbol scip-fortran . f2003-oop . use_shapes/
!                  ^ reference local 11
!                    ^^^^^^ reference scip-fortran . f2003-oop . shapes/circle#radius.
     print *, r
!             ^ reference local 13
   end associate
   block
     integer :: tmp
!               ^^^ definition local 14
!                   kind Variable
!                   display_name tmp
!                   signature_documentation
!                   > integer :: tmp
!                   enclosing_symbol scip-fortran . f2003-oop . use_shapes/
     tmp = 3
!    ^^^ reference local 14
     print *, tmp
!             ^^^ reference local 14
   end block
 end program use_shapes
!            ^^^^^^^^^^ reference scip-fortran . f2003-oop . use_shapes/
!                     ⌃ enclosing_range_end scip-fortran . f2003-oop . use_shapes/
 
