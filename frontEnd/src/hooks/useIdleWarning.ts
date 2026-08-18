import { useCallback, useEffect, useState } from 'react'

// Must match the server's PlaySession:ExpiryWarning configuration.
const IDLE_WARNING_MS = 5 * 60 * 1000

export interface UseIdleWarningResult {
  idleWarning: boolean
  dismissIdleWarning: () => void
}

export function useIdleWarning(expiresAtUtc: string | null): UseIdleWarningResult {
  const [idleWarning, setIdleWarning] = useState(false)

  useEffect(() => {
    if (!expiresAtUtc) {
      setIdleWarning(false)
      return
    }

    const check = () => {
      const remaining = Date.parse(expiresAtUtc) - Date.now()
      setIdleWarning(remaining > 0 && remaining <= IDLE_WARNING_MS)
    }

    check()
    const timer = window.setInterval(check, 30_000)

    return () => window.clearInterval(timer)
  }, [expiresAtUtc])

  const dismissIdleWarning = useCallback(() => setIdleWarning(false), [])

  return { idleWarning, dismissIdleWarning }
}
