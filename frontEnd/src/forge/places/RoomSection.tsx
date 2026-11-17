import { useEffect, useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { createRoom, DirectionOffset, OppositeDirection, updateRoom, type ExitDirection, type WorldRoom } from '../api.ts'
import { Check, Section, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useDirty } from '../hooks.ts'
import type { BuildSpot } from './MapGrid.tsx'
import { ReachNote } from './reach.tsx'

/**
 * The room's name and description. Saved straight to the map. Workstream 8:
 * every room is public, so there is no access to choose.
 */
export function RoomSection({ room }: { room: WorldRoom }) {
  const { refreshWorld } = useForgeData()
  const [name, setName] = useState(room.name)
  const [description, setDescription] = useState(room.description)
  const [saved, setSaved] = useState(false)
  const action = useAction()

  useEffect(() => {
    setName(room.name)
    setDescription(room.description)
  }, [room.id, room.version, room.name, room.description])

  const dirty = name !== room.name || description !== room.description
  useDirty(`room:${room.id}`, dirty)

  async function save() {
    setSaved(false)
    const updated = await action.run(async () => {
      await updateRoom(room.id, { name: name.trim(), description: description.trim(), accessType: room.accessType, version: room.version })
      await refreshWorld()
      return true
    })
    if (updated) setSaved(true)
  }

  return (
    <Section title="Room">
      <form
        className="forge__stack"
        onSubmit={(event) => {
          event.preventDefault()
          void save()
        }}
      >
        <ReachNote room={room} />
        <TextInput label="Name" value={name} maxLength={120} onChange={setName} />
        <TextBox label="Description" value={description} rows={4} onChange={setDescription} />
        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
        {saved && !dirty && <StatusBanner tone="success">Room saved.</StatusBanner>}
        <div className="forge__actions">
          <Button type="submit" intent="primary" disabled={!dirty || !name.trim()} busy={action.busy}>
            Save room
          </Button>
          {dirty && (
            <Button
              onClick={() => {
                setName(room.name)
                setDescription(room.description)
              }}
            >
              Undo edits
            </Button>
          )}
        </div>
      </form>
    </Section>
  )
}

/** The eight neighbouring squares, as directions from the new room. */
const Around: ExitDirection[] = ['north', 'northeast', 'east', 'southeast', 'south', 'southwest', 'west', 'northwest']
const Diagonal = new Set<ExitDirection>(['northeast', 'southeast', 'southwest', 'northwest'])

/**
 * Builds a room on an empty square of the map. WF-43: it lists every exit it
 * will make — one tick per neighbouring room, both ways — with the diagonals
 * unticked; a neighbour whose way back is taken cannot be ticked.
 */
export function BuildRoomForm({
  spot,
  onBuilt,
  onCancel,
}: {
  spot: BuildSpot
  onBuilt: (room: WorldRoom) => void
  onCancel: () => void
}) {
  const { world, refreshWorld } = useForgeData()
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [districtId, setDistrictId] = useState('')
  const action = useAction()

  const neighbours = Around.flatMap((direction) => {
    const { dx, dy } = DirectionOffset[direction]
    const room = world.rooms.find(
      (candidate) => candidate.mapLayer === spot.layer && candidate.mapX === spot.x + dx && candidate.mapY === spot.y + dy,
    )
    if (!room) return []
    const back = OppositeDirection[direction]
    const holder = world.exits.find((exit) => exit.sourceRoomId === room.id && exit.direction === back)
    return [{ direction, room, holder }]
  })
  const [joins, setJoins] = useState<ReadonlySet<ExitDirection>>(
    () => new Set(neighbours.filter((neighbour) => !neighbour.holder && !Diagonal.has(neighbour.direction)).map((neighbour) => neighbour.direction)),
  )
  const district = (world.districts ?? []).find((candidate) => candidate.id === districtId)

  async function build() {
    const room = await action.run(async () => {
      const created = await createRoom({
        name: name.trim(),
        description: description.trim(),
        accessType: 'Public',
        mapX: spot.x,
        mapY: spot.y,
        mapLayer: spot.layer,
        mapId: spot.mapId,
        districtId: districtId || null,
        joins: [...joins],
      })
      await refreshWorld()
      return created
    })
    if (room) onBuilt(room)
  }

  return (
    <form
      className="forge__build"
      aria-label="Build a room"
      onSubmit={(event) => {
        event.preventDefault()
        void build()
      }}
    >
      <p className="forge__eyebrow">
        New room ·{' '}
        {spot.from ? `${spot.from.direction} of ${spot.from.room.name}` : `at ${spot.x}, ${spot.y}`} · layer {spot.layer}
      </p>
      <TextInput label="Name" value={name} maxLength={120} autoFocus onChange={setName} />
      <TextBox
        label="Description"
        value={description}
        rows={2}
        placeholder={district?.defaultDescription ? `Blank shows ${district.name}'s: ${district.defaultDescription}` : undefined}
        onChange={setDescription}
      />
      {(world.districts ?? []).length > 0 && (
        <Select
          label="District"
          value={districtId}
          placeholder="No district"
          options={(world.districts ?? []).map((candidate) => ({ id: candidate.id, displayName: candidate.name }))}
          onChange={setDistrictId}
        />
      )}
      {neighbours.length > 0 && (
        <fieldset className="forge__fieldset forge__fieldset--boxed">
          <legend>Exits it will make, both ways</legend>
          {neighbours.map(({ direction, room, holder }) => (
            <div key={direction}>
              <Check
                label={`${direction} to ${room.name} and back`}
                checked={joins.has(direction)}
                disabled={holder !== undefined}
                onChange={(on) =>
                  setJoins((current) => {
                    const next = new Set(current)
                    if (on) next.add(direction)
                    else next.delete(direction)
                    return next
                  })
                }
              />
              {holder && (
                <span className="forge__muted">
                  {' '}
                  {room.name} already has an exit {OppositeDirection[direction]}, to {holder.destinationRoomName}.
                </span>
              )}
            </div>
          ))}
        </fieldset>
      )}
      <p className="forge__hint">The room is built as a draft: players cannot reach it until you publish it.</p>
      {action.error && (
        <StatusBanner tone="danger" role="alert">
          {action.error}
        </StatusBanner>
      )}
      <div className="forge__actions">
        <Button type="submit" intent="primary" busy={action.busy} disabled={!name.trim()}>
          Create room
        </Button>
        <Button onClick={onCancel} disabled={action.busy}>
          Cancel
        </Button>
      </div>
    </form>
  )
}
