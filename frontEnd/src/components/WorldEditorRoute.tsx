import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAccount } from '../auth/useAccount.ts'
import { Panel } from './ui/Panel.tsx'

export function WorldEditorRoute() {
  const { account } = useAccount()
  const location = useLocation()

  if (account === null) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  if (!account.roles.some((role) => role === 'Administrator' || role === 'WorldBuilder')) {
    return (
      <Panel title="Access denied">
        <div className="ui-panel__body">
          <p role="alert" className="form__error">You do not have permission to view this page.</p>
        </div>
      </Panel>
    )
  }

  return <Outlet />
}
