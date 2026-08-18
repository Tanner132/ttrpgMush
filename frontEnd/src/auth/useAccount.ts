import { useContext } from 'react'
import { AccountContext, type AccountContextValue } from './accountContext.ts'

export function useAccount(): AccountContextValue {
  const context = useContext(AccountContext)
  if (context === null) {
    throw new Error('useAccount must be used within an AccountProvider.')
  }
  return context
}
