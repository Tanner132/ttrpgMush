import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { deleteRoom, getRoomDeletion, type WorldRoom } from '../api.ts'
import { Chip, Empty, Section, Select } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useLoad } from '../hooks.ts'
import type { HereCounts } from './WorldContents.tsx'

function plural(count: number, one: string, many = `${one}s`) {
  return `${count} ${count === 1 ? one : many}`
}

/**
 * Which jobs use this room, and deleting it. The server's deletion check is
 * the one source for both: it names every job that happens in the room,
 * listens in it, puts an item in it, sends runners to it or uses an NPC
 * standing in it — exactly what stops a delete.
 */
export function UsageSection({ room, here, onDeleted }: { room: WorldRoom; here?: HereCounts | null; onDeleted: () => void }) {
  const { world, refreshWorld } = useForgeData()
  // WF-31: the check is read again whenever what Here shows changes, and the
  // counts come from what Here shows, so the two never disagree.
  const check = useLoad((signal) => getRoomDeletion(room.id, signal), [
    room.id,
    room.version,
    world.exits.length,
    world.rooms.length,
    here?.npcs,
    here?.inspectables,
    here?.items,
  ])
  const [confirming, setConfirming] = useState(false)
  const [relocateTo, setRelocateTo] = useState('')
  const action = useAction()
  const usage = check.data

  // WF-60: nobody is put in a draft room.
  const others = world.rooms
    .filter((candidate) => candidate.id !== room.id && candidate.isLive)
    .map((candidate) => ({ id: candidate.id, displayName: candidate.name }))

  async function remove() {
    const result = await action.run(() => deleteRoom(room.id, usage?.charactersPresent ? relocateTo : undefined))
    if (!result) return
    if (!result.canDelete) {
      action.setError(result.reason ?? 'The room could not be deleted.')
      setConfirming(false)
      return
    }
    await refreshWorld()
    onDeleted()
  }

  const touching = (usage?.jobs ?? []).map((name) => ({ name, how: 'Uses this room' }))

  return (
    <>
      <Section title="Jobs touching this room">
        {check.error && <StatusBanner tone="danger">{check.error}</StatusBanner>}
        {usage && touching.length === 0 && <Empty>No job uses this room.</Empty>}
        <ul className="forge__rows">
          {touching.map((entry) => (
            <li key={`${entry.name}-${entry.how}`} className="forge__row">
              <Chip tone="info">Job</Chip>
              <b className="forge__row-name">{entry.name}</b>
              <span className="forge__muted">{entry.how}</span>
            </li>
          ))}
        </ul>
      </Section>

      <Section title="Delete room">
        {!usage ? (
          <p className="forge__hint">Checking what uses this room…</p>
        ) : !usage.canDelete ? (
          <StatusBanner tone="warning">{usage.reason ?? "This room can't be deleted."}</StatusBanner>
        ) : (
          <div className="forge__stack">
            <p className="forge__hint">
              Deleting removes the room and everything in it:{' '}
              {[
                plural(usage.incomingExits + usage.outgoingExits, 'exit'),
                plural(here?.npcs ?? usage.npcsRemoved, 'NPC'),
                plural(here?.items ?? usage.itemsRemoved, 'item'),
                plural(here?.inspectables ?? usage.inspectablesRemoved, 'inspectable'),
                usage.hasRoomEvents ? 'its events' : null,
                usage.chatMessages > 0 ? plural(usage.chatMessages, 'chat message') : null,
              ]
                .filter(Boolean)
                .join(', ')}
              .
            </p>
            {usage.charactersPresent > 0 && (
              <Select
                label={`${plural(usage.charactersPresent, 'character')} standing here will move to`}
                value={relocateTo}
                options={others}
                placeholder="Choose a room"
                onChange={setRelocateTo}
              />
            )}
            {action.error && (
              <StatusBanner tone="danger" role="alert">
                {action.error}
              </StatusBanner>
            )}
            {confirming ? (
              <div className="forge__actions">
                <span className="forge__hint">Delete {room.name} for good?</span>
                <Button intent="danger" busy={action.busy} onClick={() => void remove()} aria-label={`Confirm delete ${room.name}`}>
                  Yes, delete
                </Button>
                <Button onClick={() => setConfirming(false)} disabled={action.busy}>
                  Keep it
                </Button>
              </div>
            ) : (
              <div className="forge__actions">
                <Button
                  intent="danger"
                  disabled={usage.charactersPresent > 0 && relocateTo === ''}
                  onClick={() => {
                    action.setError(null)
                    setConfirming(true)
                  }}
                >
                  Delete room…
                </Button>
              </div>
            )}
          </div>
        )}
      </Section>
    </>
  )
}
