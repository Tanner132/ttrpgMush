import { useCallback, useRef, useState } from 'react'
import type { CharacterSummary } from '../api/roomSession.ts'
import type { RoomPresence, RoomCharacterEvent } from '../realtime/presence.ts'

export interface UseRoomPresenceResult {
  occupants: CharacterSummary[]
  onlineCharacters: CharacterSummary[]
  onPresence: (presence: RoomPresence) => void
  onCharacterArrived: (event: RoomCharacterEvent) => void
  onCharacterDeparted: (event: RoomCharacterEvent) => void
  syncRoom: (roomId: string, occupants: CharacterSummary[]) => void
  clear: () => void
}

export function useRoomPresence(): UseRoomPresenceResult {
  const [occupants, setOccupants] = useState<CharacterSummary[]>([])
  const [onlineCharacters, setOnlineCharacters] = useState<CharacterSummary[]>([])

  const roomIdRef = useRef<string | null>(null)
  const appliedRevisionRef = useRef(-1)
  const bufferedPresenceRef = useRef<RoomPresence | null>(null)

  const applyPresence = useCallback((presence: RoomPresence) => {
    const roomId = roomIdRef.current
    if (roomId === null) {
      bufferedPresenceRef.current = presence
      return
    }
    if (presence.roomId !== roomId) return
    if (presence.revision < appliedRevisionRef.current) return
    appliedRevisionRef.current = presence.revision
    setOnlineCharacters(presence.onlineCharacters)
  }, [])

  const syncRoom = useCallback(
    (roomId: string, nextOccupants: CharacterSummary[]) => {
      const roomChanged = roomIdRef.current !== roomId
      roomIdRef.current = roomId

      if (roomChanged) {
        appliedRevisionRef.current = -1
        setOnlineCharacters([])
      }

      setOccupants(nextOccupants)

      const buffered = bufferedPresenceRef.current
      if (buffered !== null) {
        bufferedPresenceRef.current = null
        applyPresence(buffered)
      }
    },
    [applyPresence],
  )

  const clear = useCallback(() => {
    roomIdRef.current = null
    appliedRevisionRef.current = -1
    bufferedPresenceRef.current = null
    setOnlineCharacters([])
  }, [])

  const onPresence = useCallback((presence: RoomPresence) => applyPresence(presence), [applyPresence])

  const onCharacterArrived = useCallback((event: RoomCharacterEvent) => {
    if (event.roomId !== roomIdRef.current) return
    setOccupants((prev) =>
      prev.some((occupant) => occupant.id === event.character.id) ? prev : [...prev, event.character],
    )
  }, [])

  const onCharacterDeparted = useCallback((event: RoomCharacterEvent) => {
    if (event.roomId !== roomIdRef.current) return
    setOccupants((prev) =>
      prev.some((occupant) => occupant.id === event.character.id)
        ? prev.filter((occupant) => occupant.id !== event.character.id)
        : prev,
    )
  }, [])

  return { occupants, onlineCharacters, onPresence, onCharacterArrived, onCharacterDeparted, syncRoom, clear }
}
