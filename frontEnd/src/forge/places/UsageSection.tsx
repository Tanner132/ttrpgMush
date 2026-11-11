import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { deleteRoom, getRoomDeletion, type WorldRoom } from '../api.ts'
import { Chip, Empty, Section, Select } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useLoad } from '../hooks.ts'

function plural(count: number, one: string, many = `${one}s`) {
  return `${count} ${count === 1 ? one : many}`
}

/**
 * Which jobs use this room, and deleting it. The server's deletion check is
 * the one source for both: it names the job sites built from the room, the
 * jobs listening in it and the jobs whose way in starts here — exactly what
 * stops a delete.
 */
export function UsageSection({ room, onDeleted }: { room: WorldRoom; onDeleted: () => void }) {
  const { world, refreshWorld } = useForgeData()
  const check = useLoad((signal) => getRoomDeletion(room.id, signal), [room.id, room.version, world.exits.length, world.rooms.length])
  const [confirming, setConfirming] = useState(false)
  const [relocateTo, setRelocateTo] = useState('')
  const action = useAction()
  const usage = check.data

  const others = world.rooms
    .filter((candidate) => candidate.id !== room.id && candidate.accessType === 'Public')
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

  const touching = usage
    ? [
        ...(usage.jobSites ?? []).map((name) => ({ name, how: 'Job site is built from this room' })),
        ...(usage.listeningJobs ?? []).map((name) => ({ name, how: 'Listens to events here' })),
        ...usage.missionEntryLinks.map((name) => ({ name, how: 'Its way in starts here' })),
      ]
    : []

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
                plural(usage.npcsRemoved, 'NPC'),
                plural(usage.itemsRemoved, 'item'),
                plural(usage.inspectablesRemoved, 'inspectable'),
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
                placeholder="Choose a public room"
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
