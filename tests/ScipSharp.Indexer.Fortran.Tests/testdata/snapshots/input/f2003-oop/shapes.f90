!> Object-oriented shapes (Fortran 2003).
module shapes
  use, intrinsic :: iso_fortran_env, only: wp => real64
  implicit none
  private
  public :: shape, circle, square, area_iface, make_circle

  !> Abstract base type.
  type, abstract :: shape
    character(len=:), allocatable :: name
  contains
    procedure(area_iface), deferred :: area
    procedure :: describe => shape_describe
    generic :: info => describe
  end type shape

  abstract interface
    pure function area_iface(self) result(a)
      import :: shape, wp
      class(shape), intent(in) :: self
      real(wp) :: a
    end function area_iface
  end interface

  type, extends(shape) :: circle
    real(wp) :: radius = 1.0_wp
  contains
    procedure :: area => circle_area
  end type circle

  type, extends(shape) :: square
    real(wp) :: side
  contains
    procedure :: area => square_area
    procedure :: describe => square_describe
  end type square

  !> Callback type for visiting shapes.
  procedure(area_iface), pointer :: measure => null()

contains

  pure function circle_area(self) result(a)
    class(circle), intent(in) :: self
    real(wp) :: a
    a = 3.14159_wp * self%radius**2
  end function circle_area

  pure function square_area(self) result(a)
    class(square), intent(in) :: self
    real(wp) :: a
    a = self%side**2
  end function square_area

  subroutine shape_describe(self)
    class(shape), intent(in) :: self
    print *, self%name, self%area()
  end subroutine shape_describe

  subroutine square_describe(self)
    class(square), intent(in) :: self
    print *, 'square of side', self%side
  end subroutine square_describe

  function make_circle(r) result(c)
    real(wp), intent(in) :: r
    type(circle) :: c
    c%radius = r
    c%name = 'circle'
  end function make_circle

end module shapes

program use_shapes
  use shapes
  implicit none
  class(shape), allocatable :: s
  type(circle) :: c

  c = make_circle(2.0d0)
  allocate(s, source=c)
  call s%info()
  select type (p => s)
  type is (circle)
    print *, p%radius
  class is (square)
    print *, p%side
  class default
    print *, 'unknown'
  end select
  associate (r => c%radius)
    print *, r
  end associate
  block
    integer :: tmp
    tmp = 3
    print *, tmp
  end block
end program use_shapes
