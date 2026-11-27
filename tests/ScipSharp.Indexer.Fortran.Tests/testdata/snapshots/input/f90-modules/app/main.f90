program demo
  use vectors
  use geometry_kinds, only: dp, pi
  implicit none
  type(vec3) :: a, b, c
  real(dp) :: n
  integer :: i, ios
  real(dp), dimension(3) :: arr = [1.0_dp, 2.0_dp, 3.0_dp]

  a = vec3(1.0_dp, 0.0_dp, 0.0_dp)
  b = 2.0_dp
  c = a + b
  n = norm(c) + norm(arr) + (a .dot. b)
  outer: do i = 1, 3
    if (arr(i) > pi) exit outer
    call report(i, arr(i))
  end do outer
  open (unit=10, file='params.nml', status='old', iostat=ios)
  read (10, nml=vecparams, iostat=ios)
  write (*, 100) n
100 format (1x, 'norm = ', f10.4)

contains

  subroutine report(k, value)
    integer, intent(in) :: k
    real(dp), intent(in) :: value
    print '(i3, 1x, f8.3)', k, value + c%x
  end subroutine report

end program demo
