!⌄ enclosing_range_start scip-fortran . f2008-submodules . stack_mod/stack_impl/
 submodule (stack_mod) stack_impl
!           ^^^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/
!                      ^^^^^^^^^^ definition scip-fortran . f2008-submodules . stack_mod/stack_impl/
!                                 kind Module
!                                 display_name stack_impl
!                                 signature_documentation
!                                 > submodule (stack_mod) stack_impl
   implicit none
 contains
   module procedure push
!                   ^^^^ definition scip-fortran . f2008-submodules . stack_mod/push().
!                        kind Function
!                        display_name push
!                        signature_documentation
!                        > module subroutine push(self, value)
!                        >   class(stack_t), intent(inout) :: self
!                        >   integer, intent(in) :: value
     if (.not. allocated(self%items)) allocate(self%items(16))
!                        ^^^^ reference local 0
!                             ^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#items.
!                                              ^^^^ reference local 0
!                                                   ^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#items.
     self%top = self%top + 1
!    ^^^^ reference local 0
!         ^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#top.
!               ^^^^ reference local 0
!                    ^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#top.
     self%items(self%top) = value
!    ^^^^ reference local 0
!         ^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#items.
!               ^^^^ reference local 0
!                    ^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#top.
!                           ^^^^^ reference local 1
   end procedure push
!                ^^^^ reference scip-fortran . f2008-submodules . stack_mod/push().
 
   module function pop(self) result(value)
!                  ^^^ definition scip-fortran . f2008-submodules . stack_mod/pop().
!                      kind Function
!                      display_name pop
!                      signature_documentation
!                      > module function pop(self) result(value)
!                      >   class(stack_t), intent(inout) :: self
!                      >   integer :: value
!                      ^^^^ definition local 2
!                           kind Parameter
!                           display_name self
!                           signature_documentation
!                           > class(stack_t), intent(inout) :: self
!                           enclosing_symbol scip-fortran . f2008-submodules . stack_mod/pop().
!                                   ^^^^^ definition local 3
!                                         kind Variable
!                                         display_name value
!                                         signature_documentation
!                                         > integer :: value
!                                         enclosing_symbol scip-fortran . f2008-submodules . stack_mod/pop().
     class(stack_t), intent(inout) :: self
!          ^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#
!                                     ^^^^ definition local 2
!                                          kind Parameter
!                                          display_name self
!                                          signature_documentation
!                                          > class(stack_t), intent(inout) :: self
!                                          enclosing_symbol scip-fortran . f2008-submodules . stack_mod/pop().
     integer :: value
!               ^^^^^ definition local 3
!                     kind Variable
!                     display_name value
!                     signature_documentation
!                     > integer :: value
!                     enclosing_symbol scip-fortran . f2008-submodules . stack_mod/pop().
     value = self%items(self%top)
!    ^^^^^ reference local 3
!            ^^^^ reference local 2
!                 ^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#items.
!                       ^^^^ reference local 2
!                            ^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#top.
     self%top = self%top - 1
!    ^^^^ reference local 2
!         ^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#top.
!               ^^^^ reference local 2
!                    ^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#top.
   end function pop
!               ^^^ reference scip-fortran . f2008-submodules . stack_mod/pop().
 end submodule stack_impl
!              ^^^^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_impl/
!                       ⌃ enclosing_range_end scip-fortran . f2008-submodules . stack_mod/stack_impl/
 
!⌄ enclosing_range_start scip-fortran . f2008-submodules . stack_mod/stack_debug/
 submodule (stack_mod:stack_impl) stack_debug
!           ^^^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/
!                     ^^^^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_impl/
!                                 ^^^^^^^^^^^ definition scip-fortran . f2008-submodules . stack_mod/stack_debug/
!                                             kind Module
!                                             display_name stack_debug
!                                             signature_documentation
!                                             > submodule (stack_mod:stack_impl) stack_debug
 contains
!  ⌄ enclosing_range_start scip-fortran . f2008-submodules . stack_mod/stack_debug/dump().
   subroutine dump(s)
!             ^^^^ definition scip-fortran . f2008-submodules . stack_mod/stack_debug/dump().
!                  kind Function
!                  display_name dump
!                  signature_documentation
!                  > subroutine dump(s)
!                  >   type(stack_t), intent(in) :: s
!                  ^ definition local 4
!                    kind Parameter
!                    display_name s
!                    signature_documentation
!                    > type(stack_t), intent(in) :: s
!                    enclosing_symbol scip-fortran . f2008-submodules . stack_mod/stack_debug/dump().
     type(stack_t), intent(in) :: s
!         ^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#
!                                 ^ definition local 4
!                                   kind Parameter
!                                   display_name s
!                                   signature_documentation
!                                   > type(stack_t), intent(in) :: s
!                                   enclosing_symbol scip-fortran . f2008-submodules . stack_mod/stack_debug/dump().
     integer :: k
!               ^ definition local 5
!                 kind Variable
!                 display_name k
!                 signature_documentation
!                 > integer :: k
!                 enclosing_symbol scip-fortran . f2008-submodules . stack_mod/stack_debug/dump().
     do concurrent (k = 1:s%top)
!                   ^ reference local 5
!                         ^ reference local 4
!                           ^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#top.
       print *, s%items(k)
!               ^ reference local 4
!                 ^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_t#items.
!                       ^ reference local 5
     end do
   end subroutine dump
!                 ^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_debug/dump().
!                    ⌃ enclosing_range_end scip-fortran . f2008-submodules . stack_mod/stack_debug/dump().
 end submodule stack_debug
!              ^^^^^^^^^^^ reference scip-fortran . f2008-submodules . stack_mod/stack_debug/
!                        ⌃ enclosing_range_end scip-fortran . f2008-submodules . stack_mod/stack_debug/
 
