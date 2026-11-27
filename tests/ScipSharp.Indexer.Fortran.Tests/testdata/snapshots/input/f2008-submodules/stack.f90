!> A stack whose procedures are implemented in a submodule (Fortran 2008).
module stack_mod
  implicit none
  private
  public :: stack_t

  type :: stack_t
    integer, allocatable :: items(:)
    integer :: top = 0
  contains
    procedure :: push
    procedure :: pop
  end type stack_t

  interface
    module subroutine push(self, value)
      class(stack_t), intent(inout) :: self
      integer, intent(in) :: value
    end subroutine push
    module function pop(self) result(value)
      class(stack_t), intent(inout) :: self
      integer :: value
    end function pop
  end interface

  enum, bind(c)
    enumerator :: red = 1, green, blue
  end enum
end module stack_mod
