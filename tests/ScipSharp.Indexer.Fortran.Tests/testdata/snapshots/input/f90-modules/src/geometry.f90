!> Basic geometry kinds and helpers.
module geometry_kinds
  implicit none
  private
  integer, parameter, public :: dp = kind(1.0d0)  !! double precision kind
  real(dp), parameter, public :: pi = 3.141592653589793_dp
  public :: deg2rad
contains
  !> Convert degrees to radians.
  pure elemental function deg2rad(deg) result(rad)
    real(dp), intent(in) :: deg  !! angle in degrees
    real(dp) :: rad
    rad = deg * pi / 180.0_dp
  end function deg2rad
end module geometry_kinds

module vectors
  use geometry_kinds, only: dp, to_rad => deg2rad
  implicit none

  !> A 3D vector.
  type :: vec3
    real(dp) :: x = 0, y = 0, z = 0
  end type vec3

  interface operator(+)
    module procedure vec_add
  end interface

  interface operator(.dot.)
    module procedure vec_dot
  end interface

  !> Overloaded norm.
  interface norm
    module procedure norm_vec, norm_array
  end interface norm

  interface assignment(=)
    module procedure vec_from_scalar
  end interface

  real(dp), save :: origin_offset
  namelist /vecparams/ origin_offset

contains

  pure function vec_add(a, b) result(c)
    type(vec3), intent(in) :: a, b
    type(vec3) :: c
    c%x = a%x + b%x
    c%y = a%y + b%y
    c%z = a%z + b%z
  end function

  pure real(dp) function vec_dot(a, b)
    type(vec3), intent(in) :: a, b
    vec_dot = a%x*b%x + a%y*b%y + a%z*b%z
  end function vec_dot

  real(dp) function norm_vec(v)
    type(vec3), intent(in) :: v
    norm_vec = sqrt(v .dot. v)
  end function

  real(dp) function norm_array(v)
    real(dp), intent(in) :: v(:)
    norm_array = sqrt(sum(v**2))
  end function

  subroutine vec_from_scalar(v, s)
    type(vec3), intent(out) :: v
    real(dp), intent(in) :: s
    v = vec3(s, s, to_rad(s))
  end subroutine

end module vectors
