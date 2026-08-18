import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useGameplaySession } from '../hooks/useGameplaySession.ts'
import { useIdleWarning } from '../hooks/useIdleWarning.ts'
import { useRoomPresence } from '../hooks/useRoomPresence.ts'
import { useTranscript } from '../hooks/useTranscript.ts'
import { useRoomChat } from '../realtime/useRoomChat.ts'
import { useGameplayCommands } from '../commands/useGameplayCommands.ts'
import type { RoomSession } from '../api/roomSession.ts'
import { Composer } from '../components/Composer.tsx'
import { ConnectionStatus } from '../components/ConnectionStatus.tsx'
import { IdleWarning } from '../components/IdleWarning.tsx'
import { OccupantList } from '../components/OccupantList.tsx'
import { RoomDetails } from '../components/RoomDetails.tsx'
import { Transcript } from '../components/Transcript.tsx'
import { Panel } from '../components/ui/Panel.tsx'
import { Button } from '../components/ui/Button.tsx'

export default function GameplayPage() {
  const navigate = useNavigate()

  const {
    occupants,
    onlineCharacters,
    onPresence,
    onCharacterArrived,
    onCharacterDeparted,
    syncRoom,
    clear: clearPresence,
  } = useRoomPresence()
  const { entries, olderCursor, loadingOlder, paginationError, applySession, merge, appendLocal, loadOlder } =
    useTranscript()

  const [reconnected, setReconnected] = useState(false)
  const reconnectedTimerRef = useRef<number | null>(null)

  const handleSessionEnded = useCallback(() => {
    clearPresence()
    navigate('/characters', { replace: true })
  }, [clearPresence, navigate])

  const handleSessionReceived = useCallback(
    (next: RoomSession) => {
      syncRoom(next.room.id, next.occupants)
      applySession(next.messages, next.olderMessagesCursor)
    },
    [syncRoom, applySession],
  )

  const { session, loading, error, retry, refresh, receiveSession, expiresAtUtc, setExpiresAtUtc } = useGameplaySession({
    onSessionEnded: handleSessionEnded,
    onSessionReceived: handleSessionReceived,
  })

  const roomChat = useRoomChat({
    onMessage: (message) => merge([message]),
    onActivityExpiry: (atUtc) => {
      setExpiresAtUtc((prev) => (!prev || Date.parse(atUtc) >= Date.parse(prev) ? atUtc : prev))
    },
    onSessionExpired: handleSessionEnded,
    onRoomChanged: receiveSession,
    onCharacterArrived,
    onCharacterDeparted,
    onPresence,
    onReconnected: () => {
      setReconnected(true)
      if (reconnectedTimerRef.current !== null) window.clearTimeout(reconnectedTimerRef.current)
      reconnectedTimerRef.current = window.setTimeout(() => setReconnected(false), 4000)
      void refresh()
    },
  })

  const { state: chatState, joined, sending, sendError, rolling, moving, moveError, sendMessage, rollDice, moveThroughExit, queryOnlineCharacters, recordActivity } =
    roomChat

  useEffect(() => {
    if (chatState !== 'connected' || !joined) {
      clearPresence()
    }
  }, [chatState, joined, clearPresence])

  useEffect(() => {
    if (!joined) return

    const onActivity = () => void recordActivity()
    const events = ['keydown', 'pointerdown', 'focus'] as const

    events.forEach((event) => window.addEventListener(event, onActivity, { passive: true }))

    return () => {
      events.forEach((event) => window.removeEventListener(event, onActivity))
    }
  }, [joined, recordActivity])

  useEffect(() => {
    return () => {
      if (reconnectedTimerRef.current !== null) window.clearTimeout(reconnectedTimerRef.current)
    }
  }, [])

  const { idleWarning, dismissIdleWarning } = useIdleWarning(expiresAtUtc)

  const { submit } = useGameplayCommands({
    session,
    occupants,
    onlineCharacters,
    joined,
    sendMessage,
    rollDice,
    moveThroughExit,
    queryOnlineCharacters,
    appendLocal,
  })

  const handleRemainSignedIn = useCallback(async () => {
    if (await recordActivity(true)) dismissIdleWarning()
  }, [recordActivity, dismissIdleWarning])

  const handleSend = useCallback((content: string) => submit(content), [submit])

  const handleMove = useCallback(
    (exitId: string) => {
      void moveThroughExit(exitId)
    },
    [moveThroughExit],
  )

  if (loading) {
    return <p className="app__status">Loading…</p>
  }

  if (error) {
    return (
      <Panel title="Unable to load the room">
        <div className="ui-panel__body">
          <p role="alert" className="form__error">
            {error}
          </p>
          <Button onClick={retry}>Retry</Button>
        </div>
      </Panel>
    )
  }

  const room = session?.room
  const composerEnabled = joined && chatState === 'connected'
  const canMove = composerEnabled && !moving

  return (
    <div className="app__body">
      <main className="app__main">
        <p className="app__status">
          Playing as <strong>{session?.character.name}</strong>
        </p>

        <ConnectionStatus state={chatState} reconnected={reconnected} />

        {idleWarning && <IdleWarning onRemainSignedIn={() => void handleRemainSignedIn()} />}

        <Transcript
          roomId={room?.id ?? null}
          entries={entries}
          loadingOlder={loadingOlder}
          paginationError={paginationError}
          hasOlder={olderCursor !== null}
          onLoadOlder={loadOlder}
        />

        <Composer enabled={composerEnabled} sending={sending || rolling} sendError={sendError} onSend={handleSend} />
      </main>

      <aside className="app__sidebar">
        <RoomDetails
          room={room ?? null}
          exits={session?.exits ?? []}
          disabled={!canMove}
          moveError={moveError}
          onMove={handleMove}
        />
        <OccupantList occupants={occupants} onlineCharacters={onlineCharacters} />
      </aside>
    </div>
  )
}
