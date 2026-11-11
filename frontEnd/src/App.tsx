import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AppShell } from './components/AppShell.tsx'
import { AdminRoute } from './components/AdminRoute.tsx'
import { ProtectedRoute } from './components/ProtectedRoute.tsx'
import { WorldEditorRoute } from './components/WorldEditorRoute.tsx'
import LoginPage from './pages/LoginPage.tsx'
import CharactersPage from './pages/CharactersPage.tsx'
import GameplayPage from './pages/GameplayPage.tsx'
import NotFoundPage from './pages/NotFoundPage.tsx'
import { useAccount } from './auth/useAccount.ts'

const AdminUsersPage = lazy(() => import('./pages/admin/AdminUsersPage.tsx'))
const AdminAuditPage = lazy(() => import('./pages/admin/AdminAuditPage.tsx'))
const ForgeShell = lazy(() => import('./forge/ForgeShell.tsx'))
const ForgeHome = lazy(() => import('./forge/ForgeHome.tsx'))
const PlacesPage = lazy(() => import('./forge/places/PlacesPage.tsx'))
const JobsPage = lazy(() => import('./forge/jobs/JobsPage.tsx'))
const JobEditorPage = lazy(() => import('./forge/jobs/JobEditor.tsx'))
const LibraryPage = lazy(() => import('./forge/library/LibraryPage.tsx'))
const MethodSelectionPage = lazy(() => import('./components/characterCreation/MethodSelectionPage.tsx'))
const CreatorShellPage = lazy(() => import('./pages/characterCreation/CreatorShellPage.tsx'))
const CharacterSheetPage = lazy(() => import('./pages/CharacterSheetPage.tsx'))

function RootRedirect() {
  const { account } = useAccount()
  return account === null ? <Navigate to="/login" replace /> : <Navigate to="/play" replace />
}

export default function App() {
  return (
    <Suspense fallback={<div role="status">Loading…</div>}>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<ProtectedRoute />}>
            <Route path="/characters" element={<CharactersPage />} />
            <Route path="/characters/create" element={<MethodSelectionPage />} />
            <Route path="/characters/create/:characterId" element={<CreatorShellPage />} />
            <Route path="/characters/:characterId/sheet" element={<CharacterSheetPage />} />
            <Route path="/play" element={<GameplayPage />} />
            <Route element={<AdminRoute />}>
              <Route path="/admin" element={<Navigate to="/admin/users" replace />} />
              <Route path="/admin/users" element={<AdminUsersPage />} />
              <Route path="/admin/audit" element={<AdminAuditPage />} />
            </Route>
            <Route element={<WorldEditorRoute />}>
              {/* The World Forge redesign replaces the old world editor; its map is Places. */}
              <Route path="/admin/world" element={<Navigate to="/admin/forge/places" replace />} />
              <Route path="/admin/forge" element={<ForgeShell />}>
                <Route index element={<ForgeHome />} />
                <Route path="places" element={<PlacesPage />} />
                <Route path="jobs" element={<JobsPage />} />
                <Route path="jobs/:id" element={<JobEditorPage />} />
                <Route path="jobs/:id/:step" element={<JobEditorPage />} />
                <Route path="library" element={<Navigate to="/admin/forge/library/stat-blocks" replace />} />
                <Route path="library/stat-blocks" element={<LibraryPage shelf="stat-blocks" />} />
                <Route path="library/stat-blocks/:id" element={<LibraryPage shelf="stat-blocks" />} />
                <Route path="library/dice-tests" element={<LibraryPage shelf="dice-tests" />} />
                <Route path="library/dice-tests/:id" element={<LibraryPage shelf="dice-tests" />} />
              </Route>
            </Route>
          </Route>
          <Route path="/" element={<RootRedirect />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </Suspense>
  )
}
