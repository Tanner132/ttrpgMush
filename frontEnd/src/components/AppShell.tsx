import { useState } from 'react'
import { Outlet, useNavigate, Link } from 'react-router-dom'
import { useAccount } from '../auth/useAccount.ts'
import { toErrorMessage } from '../api/client.ts'
import { Button } from './ui/Button.tsx'
import { Panel } from './ui/Panel.tsx'

export function AppShell() {
  const { account, checking, startupError, signOut } = useAccount()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)
  const [logoutError, setLogoutError] = useState<string | null>(null)

  const isAdmin = account?.roles.includes('Administrator') ?? false
  const canEditWorld = account?.roles.some((role) => role === 'Administrator' || role === 'WorldBuilder') ?? false

  async function handleLogout() {
    setLoggingOut(true)
    setLogoutError(null)
    try {
      await signOut()
      navigate('/login', { replace: true })
    } catch (error) {
      setLogoutError(toErrorMessage(error))
    } finally {
      setLoggingOut(false)
    }
  }

  return (
    <div className="app">
      <header className="app__header">
        <h1>Seattle by Night</h1>
        {account && (
          <nav className="app__nav" aria-label="Primary">
            <Link to="/characters">Characters</Link>
            <Link to="/play">World</Link>
            {canEditWorld && <Link to="/admin/world">World editor</Link>}
            {isAdmin && <Link to="/admin/users">Admin</Link>}
          </nav>
        )}
        {account && (
          <div className="app__account">
            <span className="app__account-name">{account.userName}</span>
            <Button busy={loggingOut} onClick={() => void handleLogout()}>Log out</Button>
          </div>
        )}
      </header>

      {logoutError && (
        <p role="alert" className="form__error">
          {logoutError}
        </p>
      )}

      {checking ? (
        <p className="app__status">Loading…</p>
      ) : startupError ? (
        <Panel title="Unable to load">
          <div className="ui-panel__body">
            <p role="alert" className="form__error">
              {startupError}
            </p>
            <Button onClick={() => window.location.reload()}>Retry</Button>
          </div>
        </Panel>
      ) : (
        <Outlet />
      )}
    </div>
  )
}
