import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAccount } from '../auth/useAccount.ts'

export function ProtectedRoute() {
  const { account } = useAccount()
  const location = useLocation()

  if (account === null) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  return <Outlet />
}
