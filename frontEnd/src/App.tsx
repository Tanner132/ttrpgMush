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
const WorldEditorPage = lazy(() => import('./pages/admin/WorldEditorPage.tsx'))
const MethodSelectionPage = lazy(() => import('./components/characterCreation/MethodSelectionPage.tsx'))
const CreatorShellPage = lazy(() => import('./pages/characterCreation/CreatorShellPage.tsx'))

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
            <Route path="/play" element={<GameplayPage />} />
            <Route element={<AdminRoute />}>
              <Route path="/admin" element={<Navigate to="/admin/users" replace />} />
              <Route path="/admin/users" element={<AdminUsersPage />} />
              <Route path="/admin/audit" element={<AdminAuditPage />} />
            </Route>
            <Route element={<WorldEditorRoute />}>
              <Route path="/admin/world" element={<WorldEditorPage />} />
            </Route>
          </Route>
          <Route path="/" element={<RootRedirect />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </Suspense>
  )
}
