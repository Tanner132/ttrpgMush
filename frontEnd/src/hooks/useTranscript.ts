import { useCallback, useMemo, useRef, useState } from 'react'
import { getRoomSession, type RoomMessage } from '../api/roomSession.ts'
import { toErrorMessage } from '../api/client.ts'

export type LocalEntryKind = 'info' | 'error'

export interface LocalTranscriptEntry {
  id: string
  kind: LocalEntryKind
  text: string
}

export type TranscriptEntry =
  | { kind: 'message'; message: RoomMessage }
  | { kind: 'local'; entry: LocalTranscriptEntry }

interface StoredLocalEntry extends LocalTranscriptEntry {
  order: number
}

function mergeMessages(...groups: RoomMessage[][]): RoomMessage[] {
  const seen = new Set<string>()
  const merged: RoomMessage[] = []

  for (const group of groups) {
    for (const message of group) {
      if (seen.has(message.id)) continue
      seen.add(message.id)
      merged.push(message)
    }
  }

  merged.sort((a, b) => {
    const timestampDifference = Date.parse(a.createdAtUtc) - Date.parse(b.createdAtUtc)
    if (timestampDifference < 0) return -1
    if (timestampDifference > 0) return 1
    return a.id < b.id ? -1 : a.id > b.id ? 1 : 0
  })
  return merged
}

export interface UseTranscriptResult {
  messages: RoomMessage[]
  entries: TranscriptEntry[]
  localEntries: LocalTranscriptEntry[]
  olderCursor: string | null
  loadingOlder: boolean
  paginationError: string | null
  applySession: (messages: RoomMessage[], olderMessagesCursor: string | null) => void
  merge: (messages: RoomMessage[]) => void
  appendLocal: (kind: LocalEntryKind, text: string) => void
  loadOlder: () => Promise<boolean>
}

export function useTranscript(): UseTranscriptResult {
  const [messages, setMessages] = useState<RoomMessage[]>([])
  const [localEntries, setLocalEntries] = useState<StoredLocalEntry[]>([])
  const [olderCursor, setOlderCursor] = useState<string | null>(null)
  const [loadingOlder, setLoadingOlder] = useState(false)
  const [paginationError, setPaginationError] = useState<string | null>(null)

  const localCounterRef = useRef(0)
  const hasSessionSnapshotRef = useRef(false)

  const applySession = useCallback((incoming: RoomMessage[], cursor: string | null) => {
    setMessages((prev) => mergeMessages(prev, incoming))
    if (!hasSessionSnapshotRef.current) {
      hasSessionSnapshotRef.current = true
      setOlderCursor(cursor)
    }
  }, [])

  const merge = useCallback((incoming: RoomMessage[]) => {
    setMessages((prev) => mergeMessages(prev, incoming))
  }, [])

  const appendLocal = useCallback((kind: LocalEntryKind, text: string) => {
    const id = `local-${localCounterRef.current}`
    localCounterRef.current += 1
    const entry: StoredLocalEntry = { id, kind, text, order: Date.now() }
    setLocalEntries((prev) => [...prev, entry])
  }, [])

  const loadOlder = useCallback(async (): Promise<boolean> => {
    if (olderCursor === null || loadingOlder) return false

    setLoadingOlder(true)
    setPaginationError(null)

    try {
      const older = await getRoomSession(olderCursor)
      setMessages((prev) => mergeMessages(older.messages, prev))
      setOlderCursor(older.olderMessagesCursor)
      return true
    } catch (error) {
      setPaginationError(toErrorMessage(error))
      return false
    } finally {
      setLoadingOlder(false)
    }
  }, [olderCursor, loadingOlder])

  const entries = useMemo<TranscriptEntry[]>(() => {
    const withOrder: Array<TranscriptEntry & { order: number }> = [
      ...messages.map((message) => ({
        kind: 'message' as const,
        message,
        order: Date.parse(message.createdAtUtc),
      })),
      ...localEntries.map((entry) => ({
        kind: 'local' as const,
        entry: { id: entry.id, kind: entry.kind, text: entry.text },
        order: entry.order,
      })),
    ]

    withOrder.sort((a, b) => a.order - b.order)

    return withOrder
  }, [messages, localEntries])

  const publicLocalEntries = useMemo<LocalTranscriptEntry[]>(
    () => localEntries.map(({ id, kind, text }) => ({ id, kind, text })),
    [localEntries],
  )

  return {
    messages,
    entries,
    localEntries: publicLocalEntries,
    olderCursor,
    loadingOlder,
    paginationError,
    applySession,
    merge,
    appendLocal,
    loadOlder,
  }
}
