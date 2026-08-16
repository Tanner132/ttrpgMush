import { useCallback, useEffect, useRef, useState } from 'react'
import { HubConnectionState, type HubConnection } from '@microsoft/signalr'
import { createRoomChatConnection, type RoomChatConnectionState } from './roomChat.ts'
import type { RoomPresence, RoomCharacterEvent } from './presence.ts'
import type { RoomMessage, RoomSession } from '../api/roomSession.ts'

const ACTIVITY_THROTTLE_MS = 5 * 60 * 1000

export interface UseRoomChatHandlers {
  onMessage: (message: RoomMessage) => void
  onActivityExpiry: (expiresAtUtc: string) => void
  onSessionExpired: () => void
  onReconnected: () => void
  onRoomChanged: (session: RoomSession) => void
  onCharacterArrived: (event: RoomCharacterEvent) => void
  onCharacterDeparted: (event: RoomCharacterEvent) => void
  onPresence: (presence: RoomPresence) => void
}

export interface UseRoomChatResult {
  state: RoomChatConnectionState
  joined: boolean
  sending: boolean
  sendError: string | null
  moving: boolean
  moveError: string | null
  sendMessage: (content: string) => Promise<boolean>
  moveThroughExit: (exitId: string) => Promise<boolean>
  recordActivity: () => void
}

export function useRoomChat(handlers: UseRoomChatHandlers): UseRoomChatResult {
  const [state, setState] = useState<RoomChatConnectionState>('connecting')
  const [joined, setJoined] = useState(false)
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState<string | null>(null)
  const [moving, setMoving] = useState(false)
  const [moveError, setMoveError] = useState<string | null>(null)

  const connectionRef = useRef<HubConnection | null>(null)
  const handlersRef = useRef(handlers)
  handlersRef.current = handlers

  const sendingRef = useRef(false)
  const movingRef = useRef(false)
  const lastActivityRef = useRef(0)

  useEffect(() => {
    const connection = createRoomChatConnection()
    connectionRef.current = connection

    connection.on('MessageReceived', (message: RoomMessage) => handlersRef.current.onMessage(message))
    connection.on('SessionExpired', () => {
      setJoined(false)
      handlersRef.current.onSessionExpired()
    })
    connection.on('RoomChanged', (session: RoomSession) => handlersRef.current.onRoomChanged(session))
    connection.on('CharacterArrived', (event: RoomCharacterEvent) => handlersRef.current.onCharacterArrived(event))
    connection.on('CharacterDeparted', (event: RoomCharacterEvent) => handlersRef.current.onCharacterDeparted(event))
    connection.on('RoomPresenceChanged', (presence: RoomPresence) => handlersRef.current.onPresence(presence))

    connection.onreconnecting(() => setState('reconnecting'))
    connection.onreconnected(() => {
      setState('connected')
      void join(connection)
      handlersRef.current.onReconnected()
    })
    connection.onclose(() => setState('disconnected'))

    async function join(conn: HubConnection) {
      try {
        const presence = await conn.invoke<RoomPresence>('JoinCurrentRoom')
        handlersRef.current.onPresence(presence)
        setJoined(true)
      } catch {
        setJoined(false)
      }
    }

    async function start() {
      try {
        await connection.start()
        setState('connected')
        await join(connection)
      } catch {
        setState('disconnected')
      }
    }

    void start()

    return () => {
      void connection.stop()
      connectionRef.current = null
    }
  }, [])

  const sendMessage = useCallback(
    async (content: string): Promise<boolean> => {
      const connection = connectionRef.current

      if (!connection || connection.state !== HubConnectionState.Connected || !joined || sendingRef.current) {
        return false
      }

      sendingRef.current = true
      setSending(true)
      setSendError(null)

      try {
        const expiresAtUtc = await connection.invoke<string>('SendMessage', content)
        handlersRef.current.onActivityExpiry(expiresAtUtc)
        return true
      } catch (error) {
        setSendError(error instanceof Error ? error.message : 'Could not send message.')
        return false
      } finally {
        sendingRef.current = false
        setSending(false)
      }
    },
    [joined],
  )

  const recordActivity = useCallback(() => {
    const connection = connectionRef.current

    if (!connection || connection.state !== HubConnectionState.Connected) return

    const now = Date.now()
    if (now - lastActivityRef.current < ACTIVITY_THROTTLE_MS) return
    lastActivityRef.current = now

    void connection
      .invoke<string>('RecordActivity')
      .then((expiresAtUtc) => handlersRef.current.onActivityExpiry(expiresAtUtc))
      .catch(() => {
        // Activity renewal is best-effort; failures must not surface to the user.
      })
  }, [])

  const moveThroughExit = useCallback(
    async (exitId: string): Promise<boolean> => {
      const connection = connectionRef.current

      if (!connection || connection.state !== HubConnectionState.Connected || !joined || movingRef.current) {
        return false
      }

      movingRef.current = true
      setMoving(true)
      setMoveError(null)

      try {
        await connection.invoke('MoveThroughExit', exitId)
        return true
      } catch (error) {
        setMoveError(error instanceof Error ? error.message : 'Could not move.')
        return false
      } finally {
        movingRef.current = false
        setMoving(false)
      }
    },
    [joined],
  )

  return { state, joined, sending, sendError, moving, moveError, sendMessage, moveThroughExit, recordActivity }
}
