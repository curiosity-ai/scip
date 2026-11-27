 !> A stack whose procedures are implemented in a submodule (Fortran 2008).
!⌄ enclosing_range_start scip-fortran . f2008-submodules . stack_mod/
 module stack_mod
!       ^^^^^^^^^ definition scip-fortran . f2008-submodules . stack_mod/
!                 kind Module
!                 display_name stack_mod
!                 signature_documentation
!                 > module stack_mod
!                 documentation
!                 > A stack whose procedures are implemented in a submodule (Fortran 2008).
   implicit none
   private
   public :: stack_t
!            ^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#
 
!  ⌄ enclosing_range_start scip-fortran . f2008-submodules . stack_mod/stack_t#
   type :: stack_t
!          ^^^^^^^ definition scip-fortran . f2008-submodules . stack_mod/stack_t#
!                  kind Class
!                  display_name stack_t
!                  signature_documentation
!                  > type :: stack_t
     integer, allocatable :: items(:)
!                            ^^^^^ definition scip-fortran . f2008-submodules . stack_mod/stack_t#items.
!                                  kind Field
!                                  display_name items
!                                  signature_documentation
!                                  > integer, allocatable :: items(:)
     integer :: top = 0
!               ^^^ definition scip-fortran . f2008-submodules . stack_mod/stack_t#top.
!                   kind Field
!                   display_name top
!                   signature_documentation
!                   > integer :: top = 0
   contains
     procedure :: push
!                 ^^^^ definition scip-fortran . f2008-submodules . stack_mod/stack_t#push().
!                      kind Method
!                      display_name push
!                      signature_documentation
!                      > procedure :: push
     procedure :: pop
!                 ^^^ definition scip-fortran . f2008-submodules . stack_mod/stack_t#pop().
!                     kind Method
!                     display_name pop
!                     signature_documentation
!                     > procedure :: pop
   end type stack_t
!           ^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#
!                 ⌃ enclosing_range_end scip-fortran . f2008-submodules . stack_mod/stack_t#
 
   interface
!    ⌄ enclosing_range_start scip-fortran . f2008-submodules . stack_mod/push().
     module subroutine push(self, value)
!                      ^^^^ forward_definition scip-fortran . f2008-submodules . stack_mod/push().
!                           ^^^^ definition local 0
!                                kind Parameter
!                                display_name self
!                                signature_documentation
!                                > class(stack_t), intent(inout) :: self
!                                enclosing_symbol scip-fortran . f2008-submodules . stack_mod/push().
!                                 ^^^^^ definition local 1
!                                       kind Parameter
!                                       display_name value
!                                       signature_documentation
!                                       > integer, intent(in) :: value
!                                       enclosing_symbol scip-fortran . f2008-submodules . stack_mod/push().
       class(stack_t), intent(inout) :: self
!            ^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#
!                                       ^^^^ definition local 0
!                                            kind Parameter
!                                            display_name self
!                                            signature_documentation
!                                            > class(stack_t), intent(inout) :: self
!                                            enclosing_symbol scip-fortran . f2008-submodules . stack_mod/push().
       integer, intent(in) :: value
!                             ^^^^^ definition local 1
!                                   kind Parameter
!                                   display_name value
!                                   signature_documentation
!                                   > integer, intent(in) :: value
!                                   enclosing_symbol scip-fortran . f2008-submodules . stack_mod/push().
     end subroutine push
!                   ^^^^ reference scip-fortran . f2008-submodules . stack_mod/push().
!                      ⌃ enclosing_range_end scip-fortran . f2008-submodules . stack_mod/push().
!    ⌄ enclosing_range_start scip-fortran . f2008-submodules . stack_mod/pop().
     module function pop(self) result(value)
!                    ^^^ forward_definition scip-fortran . f2008-submodules . stack_mod/pop().
!                        ^^^^ definition local 2
!                             kind Parameter
!                             display_name self
!                             signature_documentation
!                             > class(stack_t), intent(inout) :: self
!                             enclosing_symbol scip-fortran . f2008-submodules . stack_mod/pop().
!                                     ^^^^^ definition local 3
!                                           kind Variable
!                                           display_name value
!                                           signature_documentation
!                                           > integer :: value
!                                           enclosing_symbol scip-fortran . f2008-submodules . stack_mod/pop().
       class(stack_t), intent(inout) :: self
!            ^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#
!                                       ^^^^ definition local 2
!                                            kind Parameter
!                                            display_name self
!                                            signature_documentation
!                                            > class(stack_t), intent(inout) :: self
!                                            enclosing_symbol scip-fortran . f2008-submodules . stack_mod/pop().
       integer :: value
!                 ^^^^^ definition local 3
!                       kind Variable
!                       display_name value
!                       signature_documentation
!                       > integer :: value
!                       enclosing_symbol scip-fortran . f2008-submodules . stack_mod/pop().
     end function pop
!                 ^^^ reference scip-fortran . f2008-submodules . stack_mod/pop().
!                   ⌃ enclosing_range_end scip-fortran . f2008-submodules . stack_mod/pop().
   end interface
 
   enum, bind(c)
     enumerator :: red = 1, green, blue
!                  ^^^ definition scip-fortran . f2008-submodules . stack_mod/red.
!                      kind EnumMember
!                      display_name red
!                      signature_documentation
!                      > enumerator :: red = 1
!                           ^^^^^ definition scip-fortran . f2008-submodules . stack_mod/green.
!                                 kind EnumMember
!                                 display_name green
!                                 signature_documentation
!                                 > enumerator :: green
!                                  ^^^^ definition scip-fortran . f2008-submodules . stack_mod/blue.
!                                       kind EnumMember
!                                       display_name blue
!                                       signature_documentation
!                                       > enumerator :: blue
   end enum
 end module stack_mod
!           ^^^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/
!                   ⌃ enclosing_range_end scip-fortran . f2008-submodules . stack_mod/
 
