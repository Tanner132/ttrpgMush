import { useCallback, useEffect, useRef, useState } from 'react'
import { HubConnectionState, type HubConnection } from '@microsoft/signalr'
import { createRoomChatConnection, type RoomChatConnectionState } from './roomChat.ts'
import type { RoomPresence, RoomCharacterEvent } from './presence.ts'
import type { CharacterSummary, CombatView, MessageType, RoomMessage, RoomSession } from '../api/roomSession.ts'
import type { PendingDecisionInfo } from '../api/gameActions.ts'

const ACTIVITY_THROTTLE_MS = 5 * 60 * 1000
const START_RETRY_MS = 1_000

export interface UseRoomChatHandlers {
  onMessage: (message: RoomMessage) => void
  onActivityExpiry: (expiresAtUtc: string) => void
  onSessionExpired: () => void
  onReconnected: () => void
  onRoomChanged: (session: RoomSession) => void
  onCharacterArrived: (event: RoomCharacterEvent) => void
  onCharacterDeparted: (event: RoomCharacterEvent) => void
  onPresence: (presence: RoomPresence) => void
  onCombatUpdated: (combat: CombatView) => void
  // Decisions arrive per-user, not per-room — mid-attack pauses aimed at the
  // defender (defense response, Second Chance) reach only that player.
  onDecisionRequested: (decision: PendingDecisionInfo) => void
}

export interface UseRoomChatResult {
  state: RoomChatConnectionState
  joined: boolean
  sending: boolean
  sendError: string | null
  rolling: boolean
  moving: boolean
  moveError: string | null
  sendMessage: (content: string, type: MessageType) => Promise<boolean>
  rollDice: (expression: string) => Promise<{ ok: boolean; error: string | null }>
  moveThroughExit: (exitId: string) => Promise<boolean>
  queryOnlineCharacters: () => Promise<CharacterSummary[]>
  recordActivity: (force?: boolean) => Promise<boolean>
}

export function useRoomChat(handlers: UseRoomChatHandlers): UseRoomChatResult {
  const [state, setState] = useState<RoomChatConnectionState>('connecting')
  const [joined, setJoined] = useState(false)
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState<string | null>(null)
  const [rolling, setRolling] = useState(false)
  const [moving, setMoving] = useState(false)
  const [moveError, setMoveError] = useState<string | null>(null)

  const connectionRef = useRef<HubConnection | null>(null)
  const handlersRef = useRef(handlers)
  handlersRef.current = handlers

  const sendingRef = useRef(false)
  const rollingRef = useRef(false)
  const movingRef = useRef(false)
  const lastActivityRef = useRef(0)
  const activityPromiseRef = useRef<Promise<boolean> | null>(null)

  useEffect(() => {
    const connection = createRoomChatConnection()
    connectionRef.current = connection
    let active = true
    let generation = 0
    let retryTimer: number | null = null

    connection.on('MessageReceived', (message: RoomMessage) => handlersRef.current.onMessage(message))
    connection.on('SessionExpired', () => {
      setJoined(false)
      handlersRef.current.onSessionExpired()
    })
    connection.on('RoomChanged', (session: RoomSession) => handlersRef.current.onRoomChanged(session))
    connection.on('CharacterArrived', (event: RoomCharacterEvent) => handlersRef.current.onCharacterArrived(event))
    connection.on('CharacterDeparted', (event: RoomCharacterEvent) => handlersRef.current.onCharacterDeparted(event))
    connection.on('RoomPresenceChanged', (presence: RoomPresence) => handlersRef.current.onPresence(presence))
    connection.on('CombatUpdated', (combat: CombatView) => handlersRef.current.onCombatUpdated(combat))
    connection.on('DecisionRequested', (decision: PendingDecisionInfo) => handlersRef.current.onDecisionRequested(decision))

    connection.onreconnecting(() => {
      if (!active) return
      generation += 1
      setJoined(false)
      setState('reconnecting')
    })
    connection.onreconnected(() => {
      if (!active) return
      const currentGeneration = ++generation
      setJoined(false)
      setState('reconnecting')
      void join(connection, currentGeneration, true)
    })
    connection.onclose(() => {
      if (!active) return
      generation += 1
      setJoined(false)
      setState('disconnected')
    })

    async function join(conn: HubConnection, currentGeneration: number, notifyReconnected: boolean) {
      try {
        const presence = await conn.invoke<RoomPresence>('JoinCurrentRoom')
        if (!active || currentGeneration !== generation) return
        handlersRef.current.onPresence(presence)
        setJoined(true)
        setState('connected')
        if (notifyReconnected) handlersRef.current.onReconnected()
      } catch {
        if (!active || currentGeneration !== generation) return
        setJoined(false)
        scheduleRetry(notifyReconnected)
      }
    }

    function scheduleRetry(notifyReconnected: boolean) {
      if (!active || retryTimer !== null) return
      retryTimer = window.setTimeout(() => {
        retryTimer = null
        void start(notifyReconnected)
      }, START_RETRY_MS)
    }

    async function start(notifyReconnected: boolean) {
      const currentGeneration = ++generation
      setJoined(false)
      setState('connecting')

      try {
        if (connection.state === HubConnectionState.Disconnected) await connection.start()
        if (!active || currentGeneration !== generation) return
        await join(connection, currentGeneration, notifyReconnected)
      } catch {
        if (!active || currentGeneration !== generation) return
        setState('disconnected')
        scheduleRetry(notifyReconnected)
      }
    }

    void start(false)

    return () => {
      active = false
      generation += 1
      if (retryTimer !== null) window.clearTimeout(retryTimer)
      void connection.stop()
      connectionRef.current = null
    }
  }, [])

  const sendMessage = useCallback(
    async (content: string, type: MessageType): Promise<boolean> => {
      const connection = connectionRef.current

      if (!connection || connection.state !== HubConnectionState.Connected || !joined || sendingRef.current) {
        return false
      }

      sendingRef.current = true
      setSending(true)
      setSendError(null)

      try {
        const expiresAtUtc = await connection.invoke<string>('SendMessage', content, type)
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

  const rollDice = useCallback(
    async (expression: string): Promise<{ ok: boolean; error: string | null }> => {
      const connection = connectionRef.current

      if (!connection || connection.state !== HubConnectionState.Connected || !joined || rollingRef.current) {
        return { ok: false, error: 'You are not connected.' }
      }

      rollingRef.current = true
      setRolling(true)

      try {
        const expiresAtUtc = await connection.invoke<string>('RollDice', expression)
        handlersRef.current.onActivityExpiry(expiresAtUtc)
        return { ok: true, error: null }
      } catch (error) {
        return { ok: false, error: error instanceof Error ? error.message : 'Could not roll dice.' }
      } finally {
        rollingRef.current = false
        setRolling(false)
      }
    },
    [joined],
  )

  const recordActivity = useCallback((force = false): Promise<boolean> => {
    const connection = connectionRef.current

    if (!connection || connection.state !== HubConnectionState.Connected || !joined) return Promise.resolve(false)

    const now = Date.now()
    if (!force && now - lastActivityRef.current < ACTIVITY_THROTTLE_MS) return Promise.resolve(true)
    if (activityPromiseRef.current) return activityPromiseRef.current

    const renewal = connection
      .invoke<string>('RecordActivity')
      .then((expiresAtUtc) => {
        lastActivityRef.current = Date.now()
        handlersRef.current.onActivityExpiry(expiresAtUtc)
        return true
      })
      .catch(() => false)
      .finally(() => {
        if (activityPromiseRef.current === renewal) activityPromiseRef.current = null
      })

    activityPromiseRef.current = renewal
    return renewal
  }, [joined])

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

  const queryOnlineCharacters = useCallback(async (): Promise<CharacterSummary[]> => {
    const connection = connectionRef.current

    if (!connection || connection.state !== HubConnectionState.Connected || !joined) {
      throw new Error('You are not connected.')
    }

    return connection.invoke<CharacterSummary[]>('GetOnlineCharacters')
  }, [joined])

  return {
    state,
    joined,
    sending,
    sendError,
    rolling,
    moving,
    moveError,
    sendMessage,
    rollDice,
    moveThroughExit,
    queryOnlineCharacters,
    recordActivity,
  }
}
