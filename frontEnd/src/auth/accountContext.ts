import { createContext } from 'react'
import type { Account } from '../api/account.ts'

export interface AccountContextValue {
  account: Account | null
  checking: boolean
  startupError: string | null
  signIn: (account: Account) => void
  signOut: () => Promise<void>
}

export const AccountContext = createContext<AccountContextValue | null>(null)
