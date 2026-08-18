import { useCallback, useEffect, useRef, useState, type Dispatch, type SetStateAction } from 'react'
import { ApiError, toErrorMessage } from '../api/client.ts'
import { getRoomSession, type RoomSession } from '../api/roomSession.ts'

interface UseGameplaySessionOptions {
  onSessionEnded: () => void
  onSessionReceived: (session: RoomSession) => void
}

export interface UseGameplaySessionResult {
  session: RoomSession | null
  loading: boolean
  error: string | null
  retry: () => void
  refresh: () => Promise<void>
  receiveSession: (session: RoomSession) => void
  expiresAtUtc: string | null
  setExpiresAtUtc: Dispatch<SetStateAction<string | null>>
}

export function useGameplaySession({ onSessionEnded, onSessionReceived }: UseGameplaySessionOptions): UseGameplaySessionResult {
  const [session, setSession] = useState<RoomSession | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)
  const [expiresAtUtc, setExpiresAtUtc] = useState<string | null>(null)

  const onSessionEndedRef = useRef(onSessionEnded)
  onSessionEndedRef.current = onSessionEnded
  const onSessionReceivedRef = useRef(onSessionReceived)
  onSessionReceivedRef.current = onSessionReceived
  const generationRef = useRef(0)

  const receiveSession = useCallback((next: RoomSession) => {
    generationRef.current += 1
    setSession(next)
    setExpiresAtUtc(next.expiresAtUtc)
    onSessionReceivedRef.current(next)
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const requestGeneration = ++generationRef.current

    setLoading(true)
    setError(null)

    void (async () => {
      try {
        const next = await getRoomSession(undefined, controller.signal)
        if (controller.signal.aborted || requestGeneration !== generationRef.current) return
        receiveSession(next)
      } catch (err) {
        if (controller.signal.aborted || requestGeneration !== generationRef.current) return
        if (err instanceof ApiError && err.status === 409) {
          onSessionEndedRef.current()
        } else {
          setError(toErrorMessage(err))
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    })()

    return () => controller.abort()
  }, [reloadToken, receiveSession])

  const retry = useCallback(() => setReloadToken((value) => value + 1), [])

  const refresh = useCallback(async () => {
    const requestGeneration = ++generationRef.current
    try {
      const next = await getRoomSession()
      if (requestGeneration !== generationRef.current) return
      receiveSession(next)
    } catch (err) {
      if (requestGeneration !== generationRef.current) return
      if (err instanceof ApiError && err.status === 409) {
        onSessionEndedRef.current()
      }
      // Otherwise this is a best-effort refetch; realtime delivery resumes through the reconnected socket.
    }
  }, [receiveSession])

  return { session, loading, error, retry, refresh, receiveSession, expiresAtUtc, setExpiresAtUtc }
}
