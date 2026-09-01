import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useGameplaySession } from '../hooks/useGameplaySession.ts'
import { useIdleWarning } from '../hooks/useIdleWarning.ts'
import { useRoomPresence } from '../hooks/useRoomPresence.ts'
import { useTranscript } from '../hooks/useTranscript.ts'
import { useRoomChat } from '../realtime/useRoomChat.ts'
import { useGameplayCommands } from '../commands/useGameplayCommands.ts'
import { listGameActions, performGameAction, respondToDecision } from '../api/gameActions.ts'
import type { PendingDecisionInfo } from '../api/gameActions.ts'
import type { CombatView, RoomSession } from '../api/roomSession.ts'
import { CombatStatus } from '../components/CombatStatus.tsx'
import { Composer } from '../components/Composer.tsx'
import { CharacterSheetModal } from '../components/careerSheet/CharacterSheetModal.tsx'
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

  // Latest combat snapshot wins; an inactive view (or a room without a fight)
  // clears the HUD. Decisions route through the commands hook, which is
  // created after useRoomChat needs the handler — hence the ref indirection.
  const [combat, setCombat] = useState<CombatView | null>(null)
  const receiveDecisionRef = useRef<(decision: PendingDecisionInfo) => void>(() => {})

  const handleSessionEnded = useCallback(() => {
    clearPresence()
    navigate('/characters', { replace: true })
  }, [clearPresence, navigate])

  const handleSessionReceived = useCallback(
    (next: RoomSession) => {
      syncRoom(next.room.id, next.occupants)
      applySession(next.messages, next.olderMessagesCursor)
      setCombat(next.combat)
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
    onCombatUpdated: (view) => setCombat(view.active ? view : null),
    onDecisionRequested: (decision) => receiveDecisionRef.current(decision),
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

  const [sheetOpen, setSheetOpen] = useState(false)
  const handleOpenCharacterSheet = useCallback(() => setSheetOpen(true), [])
  const handleCloseCharacterSheet = useCallback(() => setSheetOpen(false), [])

  const { submit, receiveDecision } = useGameplayCommands({
    session,
    occupants,
    onlineCharacters,
    joined,
    sendMessage,
    rollDice,
    moveThroughExit,
    queryOnlineCharacters,
    listGameActions,
    performGameAction,
    respondToDecision,
    appendLocal,
    onOpenCharacterSheet: handleOpenCharacterSheet,
  })
  receiveDecisionRef.current = receiveDecision

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
  const composerInteractive = session !== null

  return (
    <div className="grid-screen">
      <div className="grid-screen__status">
        <p className="app__status">
          Playing as <strong>{session?.character.name}</strong>
        </p>

        <ConnectionStatus state={chatState} reconnected={reconnected} />

        {idleWarning && <IdleWarning onRemainSignedIn={() => void handleRemainSignedIn()} />}
      </div>

      <div className="grid-screen__body">
        <section className="grid-transcript">
          <Transcript
            roomId={room?.id ?? null}
            roomName={room?.name ?? null}
            entries={entries}
            loadingOlder={loadingOlder}
            paginationError={paginationError}
            hasOlder={olderCursor !== null}
            onLoadOlder={loadOlder}
          />
          <Composer
            interactive={composerInteractive}
            connected={composerEnabled}
            sending={sending || rolling}
            sendError={sendError}
            characterName={session?.character.name ?? null}
            roomName={room?.name ?? null}
            onSend={handleSend}
          />
        </section>

        <aside className="grid-room">
          <RoomDetails
            room={room ?? null}
            exits={session?.exits ?? []}
            disabled={!canMove}
            moveError={moveError}
            onMove={handleMove}
          />
          <CombatStatus combat={combat} />
          <OccupantList occupants={occupants} onlineCharacters={onlineCharacters} />
        </aside>
      </div>

      {sheetOpen && session && (
        <CharacterSheetModal characterId={session.character.id} onClose={handleCloseCharacterSheet} />
      )}
    </div>
  )
}
