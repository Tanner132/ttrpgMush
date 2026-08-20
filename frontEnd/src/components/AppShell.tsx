import { useState } from 'react'
import { Outlet, useNavigate, NavLink } from 'react-router-dom'
import { useAccount } from '../auth/useAccount.ts'
import { useClock } from '../hooks/useClock.ts'
import { toErrorMessage } from '../api/client.ts'
import { Button } from './ui/Button.tsx'
import { Panel } from './ui/Panel.tsx'
import { CrtOverlay } from './ui/CrtOverlay.tsx'
import { BootScreen } from './ui/BootScreen.tsx'
import { useBootSequence } from '../hooks/useBootSequence.ts'

interface RailItem {
  to: string
  code: string
  label: string
  name: string
}

export function AppShell() {
  const { account, checking, startupError, signOut } = useAccount()
  const { booting, bootLog, bootPct, skipBoot } = useBootSequence()
  const clock = useClock()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)
  const [logoutError, setLogoutError] = useState<string | null>(null)

  const isAdmin = account?.roles.includes('Administrator') ?? false
  const canEditWorld = account?.roles.some((role) => role === 'Administrator' || role === 'WorldBuilder') ?? false

  const railItems: RailItem[] = [
    { to: '/characters', code: 'PS', label: 'Persona', name: 'Characters' },
    { to: '/play', code: 'GR', label: 'Grid', name: 'World' },
    ...(canEditWorld ? [{ to: '/admin/world', code: 'WE', label: 'W.Edit', name: 'World editor' }] : []),
    ...(isAdmin ? [{ to: '/admin/users', code: 'AD', label: 'Admin', name: 'Admin' }] : []),
    ...(isAdmin ? [{ to: '/admin/audit', code: 'AU', label: 'Audit', name: 'Audit log' }] : []),
  ]

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
      <CrtOverlay />
      {booting && <BootScreen log={bootLog} pct={bootPct} onSkip={skipBoot} />}

      <header className="app__topbar">
        <div className="app__topbar-brand">
          <span className="app__topbar-dot" aria-hidden="true" />
          <span className="app__topbar-title">Seattle by Night</span>
          <span className="app__topbar-subtitle">Fuchi Cyber-7</span>
        </div>

        <div className="app__topbar-domains" role="group" aria-label="Perception domain">
          <button type="button" className="app__topbar-domain app__topbar-domain--active" aria-pressed="true">
            Meat
          </button>
          <button type="button" className="app__topbar-domain" disabled title="Matrix decking — coming soon">
            Matrix
          </button>
          <button type="button" className="app__topbar-domain" disabled title="Astral perception — coming soon">
            Astral
          </button>
        </div>

        <div className="app__topbar-spacer" />

        {account && (
          <div className="app__account">
            {logoutError && (
              <p role="alert" className="form__error">
                {logoutError}
              </p>
            )}
            <span className="app__account-label">Persona</span>
            <span className="app__account-name">{account.userName}</span>
            <span className="app__account-clock">{clock}</span>
            <Button busy={loggingOut} onClick={() => void handleLogout()}>
              Log out
            </Button>
          </div>
        )}
      </header>

      <div className="app__frame">
        {account && (
          <nav className="app__rail" aria-label="Primary">
            {railItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                aria-label={item.name}
                className={({ isActive }) => ['app__rail-item', isActive ? 'app__rail-item--active' : null].filter(Boolean).join(' ')}
              >
                <span className="app__rail-code" aria-hidden="true">
                  {item.code}
                </span>
                <span className="app__rail-label" aria-hidden="true">
                  {item.label}
                </span>
              </NavLink>
            ))}
          </nav>
        )}

        <main className="app__content">
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
        </main>
      </div>
    </div>
  )
}
