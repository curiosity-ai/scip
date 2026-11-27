submodule (stack_mod) stack_impl
  implicit none
contains
  module procedure push
    if (.not. allocated(self%items)) allocate(self%items(16))
    self%top = self%top + 1
    self%items(self%top) = value
  end procedure push

  module function pop(self) result(value)
    class(stack_t), intent(inout) :: self
    integer :: value
    value = self%items(self%top)
    self%top = self%top - 1
  end function pop
end submodule stack_impl

submodule (stack_mod:stack_impl) stack_debug
contains
  subroutine dump(s)
    type(stack_t), intent(in) :: s
    integer :: k
    do concurrent (k = 1:s%top)
      print *, s%items(k)
    end do
  end subroutine dump
end submodule stack_debug
