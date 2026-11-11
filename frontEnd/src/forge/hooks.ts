import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import { toErrorMessage } from '../api/client.ts'

/**
 * Loads something and keeps it. Each load is numbered and only the newest
 * one's answer is kept, so switching what is open mid-fetch can never land
 * the old answer on the new screen (WF-2).
 */
export function useLoad<T>(loader: (signal: AbortSignal) => Promise<T>, deps: readonly unknown[]) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const request = useRef(0)
  const controller = useRef<AbortController | null>(null)
  const loaderRef = useRef(loader)

  useEffect(() => {
    loaderRef.current = loader
  })

  const reload = useCallback(async () => {
    const mine = ++request.current
    controller.current?.abort()
    const current = new AbortController()
    controller.current = current
    setLoading(true)
    setError(null)
    try {
      const value = await loaderRef.current(current.signal)
      if (mine === request.current) setData(value)
    } catch (caught) {
      if (mine === request.current && !current.signal.aborted) setError(toErrorMessage(caught))
    } finally {
      if (mine === request.current) setLoading(false)
    }
  }, [])

  // Any answer still in flight is for a screen that is no longer showing;
  // it is dropped when it arrives.
  const invalidate = useCallback(() => {
    request.current++
  }, [])

  // The caller names what the load depends on, as with useEffect; they are
  // compared as one key so the effect's own list stays fixed.
  const key = JSON.stringify(deps)
  useEffect(() => {
    setData(null)
    void reload()
    return invalidate
  }, [key, reload, invalidate])

  return { data, setData, error, loading, reload }
}

/** Runs one write at a time and keeps its error, for save and publish buttons. */
export function useAction() {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const run = useCallback(async <T,>(action: () => Promise<T>): Promise<T | undefined> => {
    setBusy(true)
    setError(null)
    try {
      return await action()
    } catch (caught) {
      setError(toErrorMessage(caught))
      return undefined
    } finally {
      setBusy(false)
    }
  }, [])

  return { busy, error, setError, run }
}

// ------------------------------------------------------------ unsaved edits

export interface DirtyRegistry {
  setDirty: (owner: string, dirty: boolean) => void
  /** True when there is nothing unsaved, or the author agrees to drop it. */
  confirmLeave: () => boolean
}

export const DirtyContext = createContext<DirtyRegistry>({
  setDirty: () => {},
  confirmLeave: () => true,
})

/**
 * The Forge-wide record of unsaved edits (WF-10). Moving to another room,
 * tab or record asks before throwing edits away, and so does closing the tab.
 */
export function useDirtyRegistry(): DirtyRegistry {
  const owners = useRef(new Set<string>())

  useEffect(() => {
    function warn(event: BeforeUnloadEvent) {
      if (owners.current.size > 0) event.preventDefault()
    }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [])

  const setDirty = useCallback((owner: string, dirty: boolean) => {
    if (dirty) owners.current.add(owner)
    else owners.current.delete(owner)
  }, [])

  const confirmLeave = useCallback(() => {
    if (owners.current.size === 0) return true
    const leave = window.confirm('You have unsaved changes. Leave without saving them?')
    if (leave) owners.current.clear()
    return leave
  }, [])

  return { setDirty, confirmLeave }
}

/** Marks this editor's edits as unsaved while `dirty` is true. */
export function useDirty(owner: string, dirty: boolean) {
  const { setDirty } = useContext(DirtyContext)
  useEffect(() => {
    setDirty(owner, dirty)
    return () => setDirty(owner, false)
  }, [owner, dirty, setDirty])
}

export function useConfirmLeave() {
  return useContext(DirtyContext).confirmLeave
}
