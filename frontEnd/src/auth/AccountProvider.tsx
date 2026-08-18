import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { ApiError, toErrorMessage } from '../api/client.ts'
import { getCurrentAccount, logout, type Account } from '../api/account.ts'
import { AccountContext } from './accountContext.ts'

export function AccountProvider({ children }: { children: ReactNode }) {
  const [account, setAccount] = useState<Account | null>(null)
  const [checking, setChecking] = useState(true)
  const [startupError, setStartupError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    async function restore() {
      try {
        const current = await getCurrentAccount()
        if (!cancelled) setAccount(current)
      } catch (error) {
        if (!cancelled && (!(error instanceof ApiError) || error.status !== 401)) {
          setStartupError(toErrorMessage(error))
        }
      } finally {
        if (!cancelled) setChecking(false)
      }
    }

    void restore()

    return () => {
      cancelled = true
    }
  }, [])

  const signIn = useCallback((next: Account) => {
    setAccount(next)
    setStartupError(null)
  }, [])

  const signOut = useCallback(async () => {
    await logout()
    setAccount(null)
    setStartupError(null)
  }, [])

  return <AccountContext.Provider value={{ account, checking, startupError, signIn, signOut }}>{children}</AccountContext.Provider>
}
