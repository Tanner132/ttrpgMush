import { Link, NavLink, Outlet } from 'react-router-dom'
import { ForgeContractVersion } from './api.ts'
import { ForgeDataProvider } from './ForgeData.tsx'
import { useForgeData } from './forgeContext.ts'
import { DirtyContext, useDirtyRegistry } from './hooks.ts'
import { useGuardedClick } from './navigation.ts'
import { SignInPrompt } from './SignInPrompt.tsx'

const Tabs = [
  { to: '/admin/forge/places', label: 'Places' },
  { to: '/admin/forge/jobs', label: 'Jobs' },
  { to: '/admin/forge/library', label: 'Library' },
]

function ForgeHeader() {
  const { inventory, palette } = useForgeData()
  const guard = useGuardedClick()
  const drafts = inventory.definitions.filter((definition) => definition.status === 'Draft' || definition.hasPendingEdits).length
  const errors = inventory.definitions.filter((definition) => definition.draftError).length

  return (
    <>
      <header className="forge__header">
        <Link to="/admin/forge" className="forge__brand" onClick={guard}>
          World Forge
        </Link>
        <span className="forge__spacer" />
        <span className="forge__counts">
          Drafts <b className="forge__count forge__count--draft">{drafts}</b> · Can't go live{' '}
          <b className={errors > 0 ? 'forge__count forge__count--error' : 'forge__count'}>{errors}</b>
        </span>
      </header>
      <nav className="forge__tabs" aria-label="Forge sections">
        {Tabs.map((tab) => (
          <NavLink
            key={tab.to}
            to={tab.to}
            onClick={guard}
            className={({ isActive }) => (isActive ? 'forge__tab forge__tab--active' : 'forge__tab')}
          >
            {tab.label}
          </NavLink>
        ))}
      </nav>
      {palette.contractVersion !== ForgeContractVersion && (
        <p className="forge__corpus-error" role="alert">
          This Forge and the server are different versions, so saves may be refused. Reload the page; if this stays, the server needs updating.
        </p>
      )}
      {inventory.corpusError && (
        <p className="forge__corpus-error" role="alert">
          What the game is serving no longer loads: {inventory.corpusError}
        </p>
      )}
    </>
  )
}

export default function ForgeShell() {
  const registry = useDirtyRegistry()
  return (
    <DirtyContext.Provider value={registry}>
      <div className="forge">
        <ForgeDataProvider>
          <ForgeHeader />
          <div className="forge__body">
            <Outlet />
          </div>
          <SignInPrompt />
        </ForgeDataProvider>
      </div>
    </DirtyContext.Provider>
  )
}
