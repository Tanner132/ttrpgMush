import type { ReactElement } from 'react'
import { render } from '@testing-library/react'
import { MemoryRouter, type InitialEntry } from 'react-router-dom'
import { AccountProvider } from '../auth/AccountProvider.tsx'

export function renderWithRouter(ui: ReactElement, initialEntries: InitialEntry[] = ['/']) {
  return render(<MemoryRouter initialEntries={initialEntries}>{ui}</MemoryRouter>)
}

export function renderWithProviders(ui: ReactElement, initialEntries: InitialEntry[] = ['/']) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <AccountProvider>{ui}</AccountProvider>
    </MemoryRouter>,
  )
}
