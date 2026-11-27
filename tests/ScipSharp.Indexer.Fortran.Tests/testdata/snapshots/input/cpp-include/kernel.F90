#include "config.h"
module kernel
  implicit none
#ifdef USE_DOUBLE
  integer, parameter :: rk = selected_real_kind(15)
#else
  integer, parameter :: rk = selected_real_kind(6)
#endif
  real(rk) :: scale_factor = 2.0_rk; integer :: calls = 0
contains
  subroutine apply(x, &
                   n)
    integer, intent(in) :: n
    real(rk), intent(inout) :: x(n)
    x = x * scale_factor &
          + VECTOR_LEN
    calls = calls + 1
    print *, "applied to ", n, " elements; &
             &calls so far:", calls
  end subroutine apply
end module kernel
