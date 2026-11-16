import { useState } from 'react'
import { ExitStateValues, returnStates } from '../../api/exitStates.ts'
import { Button } from '../../components/ui/Button.tsx'
import { Modal } from '../../components/ui/Modal.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import {
  createExit,
  createRoom,
  deleteExit,
  DirectionOffset,
  ExitDirections,
  OppositeDirection,
  updateExit,
  type ExitDirection,
  type ExitState,
  type WorldExit,
  type WorldRoom,
} from '../api.ts'
import { Check, Chip, Empty, Section, Segmented, Select, TextBox, TextInput } from '../fields.tsx'
import { shortDirection, words } from '../format.ts'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'
import { joinRooms } from './exits.ts'

type Editing = { exit: WorldExit } | { direction: ExitDirection }

const StateLabels: Record<ExitState, { label: string; hint: string; tone: 'info' | 'draft' | 'error' }> = {
  locked: { label: 'Locked', hint: 'Shown, and nobody gets through.', tone: 'draft' },
  hidden: { label: 'Hidden', hint: 'Not shown or usable until found.', tone: 'info' },
  restricted: { label: 'Restricted', hint: 'Nobody gets in this way; anyone inside can still leave. Set it on the doors into a room.', tone: 'error' },
  private: { label: 'Private', hint: "A player's own room. Nobody can go in until player homes exist.", tone: 'draft' },
}

function ExitFlags({ exit }: { exit: WorldExit }) {
  if (exit.states.length === 0) return <Chip>Open</Chip>
  return (
    <>
      {ExitStateValues.filter((state) => exit.states.includes(state)).map((state) => (
        <Chip key={state} tone={StateLabels[state].tone}>
          {StateLabels[state].label}
        </Chip>
      ))}
    </>
  )
}

function ExitDialog({ room, editing, onClose }: { room: WorldRoom; editing: Editing; onClose: () => void }) {
  const { world, refreshWorld } = useForgeData()
  const existing = 'exit' in editing ? editing.exit : null
  const taken = new Map(
    world.exits
      .filter((exit) => exit.sourceRoomId === room.id && exit.id !== existing?.id)
      .map((exit) => [exit.direction, exit.destinationRoomName]),
  )
  const [direction, setDirection] = useState<ExitDirection>(existing?.direction ?? ('direction' in editing ? editing.direction : 'north'))
  const [target, setTarget] = useState<'existing' | 'new'>(existing ? 'existing' : 'new')
  const [destinationId, setDestinationId] = useState(existing?.destinationRoomId ?? '')
  const [newName, setNewName] = useState('')
  const [newDescription, setNewDescription] = useState('')
  const [bothWays, setBothWays] = useState(true)
  const [states, setStates] = useState<ExitState[]>(existing?.states ?? [])
  const toggle = (state: ExitState, on: boolean) =>
    setStates((current) => ExitStateValues.filter((candidate) => (candidate === state ? on : current.includes(candidate))))
  const action = useAction()

  const offset = DirectionOffset[direction]
  const spot = { x: room.mapX + offset.dx, y: room.mapY + offset.dy, layer: room.mapLayer + offset.dz }
  const occupant = world.rooms.find((candidate) => candidate.mapX === spot.x && candidate.mapY === spot.y && candidate.mapLayer === spot.layer)
  const destinations = world.rooms
    .filter((candidate) => candidate.id !== room.id)
    .sort((a, b) => a.mapLayer - b.mapLayer || a.name.localeCompare(b.name))
    .map((candidate) => ({ id: candidate.id, displayName: `Layer ${candidate.mapLayer} › ${candidate.name}` }))

  async function save() {
    const done = await action.run(async () => {
      if (existing) {
        await updateExit(existing.id, {
          sourceRoomId: room.id,
          destinationRoomId: destinationId,
          direction,
          states,
          version: existing.version,
        })
      } else {
        if (target === 'new') {
          if (occupant) throw new Error(`${occupant.name} already stands ${direction} of here. Lead the exit to it as an existing room.`)
          const created = await createRoom({
            name: newName.trim(),
            description: newDescription.trim(),
            accessType: room.accessType,
            mapX: spot.x,
            mapY: spot.y,
            mapLayer: spot.layer,
          })
          // The server has already joined the new room to its neighbours;
          // this makes that join what was asked for here.
          await joinRooms(
            { sourceRoomId: room.id, destinationRoomId: created.id, direction, states },
            bothWays ? { sourceRoomId: created.id, destinationRoomId: room.id, direction: OppositeDirection[direction], states: returnStates(states) } : null,
          )
        } else {
          await createExit({ sourceRoomId: room.id, destinationRoomId: destinationId, direction, states })
          const reverseExists = world.exits.some((exit) => exit.sourceRoomId === destinationId && exit.destinationRoomId === room.id)
          if (bothWays && !reverseExists) {
            await createExit({
              sourceRoomId: destinationId,
              destinationRoomId: room.id,
              direction: OppositeDirection[direction],
              states: returnStates(states),
            })
          }
        }
      }
      await refreshWorld()
      return true
    })
    if (done) onClose()
  }

  const ready =
    !taken.has(direction) &&
    (existing || target === 'existing' ? destinationId !== '' : newName.trim() !== '' && newDescription.trim() !== '')

  return (
    <Modal title={existing ? 'Edit exit' : 'Add exit'} onClose={() => !action.busy && onClose()}>
      <form
        className="forge__dialog"
        onSubmit={(event) => {
          event.preventDefault()
          void save()
        }}
      >
        <p className="forge__eyebrow">From {room.name} · layer {room.mapLayer}</p>
        <fieldset className="forge__fieldset">
          <legend className="ui-field__label">Direction</legend>
          <div className="forge__directions">
            {ExitDirections.map((candidate) => (
              <button
                key={candidate}
                type="button"
                aria-pressed={direction === candidate}
                disabled={taken.has(candidate)}
                title={taken.has(candidate) ? `Taken: ${taken.get(candidate)}` : words(candidate)}
                aria-label={taken.has(candidate) ? `${words(candidate)} (taken by ${taken.get(candidate)})` : words(candidate)}
                onClick={() => setDirection(candidate)}
              >
                {shortDirection(candidate)}
              </button>
            ))}
          </div>
        </fieldset>

        {!existing && (
          <Segmented
            label="Leads to"
            value={target}
            options={[
              { id: 'new', displayName: 'New room' },
              { id: 'existing', displayName: 'Existing room' },
            ]}
            onChange={setTarget}
          />
        )}

        {existing || target === 'existing' ? (
          <Select label="Destination room" value={destinationId} options={destinations} placeholder="Choose a room" onChange={setDestinationId} />
        ) : occupant ? (
          <StatusBanner tone="warning">
            {occupant.name} already stands {direction} of here. Choose Existing room to lead the exit to it.
          </StatusBanner>
        ) : (
          <>
            <TextInput label="New room name" value={newName} maxLength={120} onChange={setNewName} />
            <TextBox label="New room description" value={newDescription} rows={2} onChange={setNewDescription} />
            <p className="forge__hint">
              Built at {spot.x}, {spot.y} on layer {spot.layer}, {room.accessType.toLowerCase()} like this room.
            </p>
          </>
        )}

        <div className="forge__checks">
          {!existing && <Check label="Both ways" checked={bothWays} onChange={setBothWays} />}
          {ExitStateValues.map((state) => (
            <Check key={state} label={StateLabels[state].label} checked={states.includes(state)} onChange={(on) => toggle(state, on)} />
          ))}
        </div>
        <p className="forge__hint">
          {states.length === 0
            ? 'An open door.'
            : ExitStateValues.filter((state) => states.includes(state))
                .map((state) => `${StateLabels[state].label}: ${StateLabels[state].hint}`)
                .join(' ')}
        </p>
        {!existing && bothWays && returnStates(states).length !== states.length && (
          <p className="forge__hint">Restricted and Private go on this direction only; the way back is left free to leave by.</p>
        )}
        {existing && <p className="forge__hint">Only this direction changes; the way back, if there is one, is its own exit.</p>}

        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
        <div className="forge__dialog-actions">
          <Button onClick={onClose} disabled={action.busy}>
            Cancel
          </Button>
          <Button type="submit" intent="primary" busy={action.busy} disabled={!ready}>
            {existing ? 'Save exit' : 'Add exit'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

/** The ways out of this room, and the ways in that have no way back. */
export function ExitsSection({ room }: { room: WorldRoom }) {
  const { world, refreshWorld } = useForgeData()
  const [editing, setEditing] = useState<Editing | null>(null)
  const [removing, setRemoving] = useState<WorldExit | null>(null)
  const remove = useAction()

  async function confirmRemove(exit: WorldExit) {
    await remove.run(async () => {
      await deleteExit(exit.id)
      await refreshWorld()
      return true
    })
    // A refusal stays on screen with the confirm put away (WF-10).
    setRemoving(null)
  }

  const outgoing = world.exits
    .filter((exit) => exit.sourceRoomId === room.id)
    .sort((a, b) => ExitDirections.indexOf(a.direction) - ExitDirections.indexOf(b.direction))
  const oneWayIn = world.exits.filter(
    (exit) =>
      exit.destinationRoomId === room.id &&
      !world.exits.some((back) => back.sourceRoomId === room.id && back.destinationRoomId === exit.sourceRoomId),
  )

  return (
    <Section title="Exits">
      {outgoing.length === 0 ? (
        <Empty>No way out yet.</Empty>
      ) : (
        <ul className="forge__rows">
          {outgoing.map((exit) => (
            <li key={exit.id} className="forge__row">
              <span className="forge__direction">{shortDirection(exit.direction)}</span>
              <span className="forge__row-name">{exit.destinationRoomName}</span>
              <ExitFlags exit={exit} />
              {removing?.id === exit.id ? (
                <>
                  <Button intent="danger" busy={remove.busy} aria-label={`Confirm remove exit ${exit.direction} to ${exit.destinationRoomName}`} onClick={() => void confirmRemove(exit)}>
                    Yes, remove
                  </Button>
                  <Button disabled={remove.busy} onClick={() => setRemoving(null)}>
                    Keep
                  </Button>
                </>
              ) : (
                <>
                  <Button aria-label={`Edit exit ${exit.direction} to ${exit.destinationRoomName}`} onClick={() => setEditing({ exit })}>
                    Edit
                  </Button>
                  <Button
                    intent="danger"
                    aria-label={`Remove exit ${exit.direction} to ${exit.destinationRoomName}`}
                    onClick={() => {
                      remove.setError(null)
                      setRemoving(exit)
                    }}
                  >
                    Remove
                  </Button>
                </>
              )}
            </li>
          ))}
        </ul>
      )}
      {oneWayIn.length > 0 && (
        <p className="forge__hint">
          One-way in, with no exit back: {oneWayIn.map((exit) => `${exit.sourceRoomName} (${exit.direction})`).join(', ')}.
        </p>
      )}
      <div className="forge__actions">
        <Button onClick={() => setEditing({ direction: ExitDirections.find((direction) => !outgoing.some((exit) => exit.direction === direction) && direction !== 'up' && direction !== 'down') ?? 'north' })}>
          + Exit
        </Button>
        <Button onClick={() => setEditing({ direction: 'up' })} disabled={outgoing.some((exit) => exit.direction === 'up')}>
          + Stairs up
        </Button>
        <Button onClick={() => setEditing({ direction: 'down' })} disabled={outgoing.some((exit) => exit.direction === 'down')}>
          + Stairs down
        </Button>
      </div>
      {remove.error && (
        <StatusBanner tone="danger" role="alert">
          {remove.error}
        </StatusBanner>
      )}
      <p className="forge__hint">Removing an exit takes away this direction only; the way back is its own exit, listed on the room it leaves from.</p>
      {editing && <ExitDialog room={room} editing={editing} onClose={() => setEditing(null)} />}
    </Section>
  )
}
