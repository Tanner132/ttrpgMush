import { render } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ForgeShell from '../ForgeShell.tsx'
import ForgeHome from '../ForgeHome.tsx'
import JobEditorPage from '../jobs/JobEditor.tsx'
import JobsPage from '../jobs/JobsPage.tsx'
import LibraryPage from '../library/LibraryPage.tsx'
import PlacesPage from '../places/PlacesPage.tsx'
import { LocationProbe } from './LocationProbe.tsx'

export function renderForge(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/admin/forge" element={<ForgeShell />}>
          <Route index element={<ForgeHome />} />
          <Route path="places" element={<PlacesPage />} />
          <Route path="jobs" element={<JobsPage />} />
          <Route path="jobs/:id" element={<JobEditorPage />} />
          <Route path="jobs/:id/:step" element={<JobEditorPage />} />
          <Route path="library/stat-blocks" element={<LibraryPage shelf="stat-blocks" />} />
          <Route path="library/stat-blocks/:id" element={<LibraryPage shelf="stat-blocks" />} />
          <Route path="library/dice-tests" element={<LibraryPage shelf="dice-tests" />} />
          <Route path="library/dice-tests/:id" element={<LibraryPage shelf="dice-tests" />} />
        </Route>
      </Routes>
      <LocationProbe />
    </MemoryRouter>,
  )
}
