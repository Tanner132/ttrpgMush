import { useEffect, useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { createRoom, OppositeDirection, updateRoom, type RoomAccessType, type WorldRoom } from '../api.ts'
import { Check, Section, Segmented, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useDirty } from '../hooks.ts'
import { joinRooms } from './exits.ts'
import type { BuildSpot } from './MapGrid.tsx'

const AccessOptions: { id: RoomAccessType; displayName: string }[] = [
  { id: 'Public', displayName: 'Public' },
  { id: 'Private', displayName: 'Private · job site' },
]

const AccessHint: Record<RoomAccessType, string> = {
  Public: 'Anyone can walk in.',
  Private: 'Part of a job site. Only runners on that job get in, each into their own copy; everyone else finds the door locked.',
}

/** The room's name, description and who may enter. Saved straight to the map. */
export function RoomSection({ room }: { room: WorldRoom }) {
  const { refreshWorld } = useForgeData()
  const [name, setName] = useState(room.name)
  const [description, setDescription] = useState(room.description)
  const [accessType, setAccessType] = useState<RoomAccessType>(room.accessType)
  const [saved, setSaved] = useState(false)
  const action = useAction()

  useEffect(() => {
    setName(room.name)
    setDescription(room.description)
    setAccessType(room.accessType)
  }, [room.id, room.version, room.name, room.description, room.accessType])

  const dirty = name !== room.name || description !== room.description || accessType !== room.accessType
  useDirty(`room:${room.id}`, dirty)

  async function save() {
    setSaved(false)
    const updated = await action.run(async () => {
      await updateRoom(room.id, { name: name.trim(), description: description.trim(), accessType, version: room.version })
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
        <TextInput label="Name" value={name} maxLength={120} onChange={setName} />
        <TextBox label="Description" value={description} rows={4} onChange={setDescription} />
        <Segmented label="Who can enter" value={accessType} options={AccessOptions} onChange={setAccessType} />
        <p className="forge__hint">{AccessHint[accessType]}</p>
        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
        {saved && !dirty && <StatusBanner tone="success">Room saved.</StatusBanner>}
        <div className="forge__actions">
          <Button type="submit" intent="primary" disabled={!dirty || !name.trim() || !description.trim()} busy={action.busy}>
            Save room
          </Button>
          {dirty && (
            <Button
              onClick={() => {
                setName(room.name)
                setDescription(room.description)
                setAccessType(room.accessType)
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

/**
 * Builds a room on an empty square of the map, and — when the square is next
 * to a room — the exit joining them, one way or both.
 */
export function BuildRoomForm({
  spot,
  access,
  onBuilt,
  onCancel,
}: {
  spot: BuildSpot
  /** Who can enter, to start with. Defaults to whatever the room it opens off is. */
  access?: RoomAccessType
  onBuilt: (room: WorldRoom) => void
  onCancel: () => void
}) {
  const { refreshWorld } = useForgeData()
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [accessType, setAccessType] = useState<RoomAccessType>(access ?? spot.from?.room.accessType ?? 'Public')
  const [bothWays, setBothWays] = useState(true)
  const action = useAction()

  async function build() {
    const room = await action.run(async () => {
      const created = await createRoom({
        name: name.trim(),
        description: description.trim(),
        accessType,
        mapX: spot.x,
        mapY: spot.y,
        mapLayer: spot.layer,
      })
      if (spot.from) {
        const { room: from, direction } = spot.from
        await joinRooms(
          { sourceRoomId: from.id, destinationRoomId: created.id, direction, isHidden: false, isLocked: false },
          bothWays
            ? { sourceRoomId: created.id, destinationRoomId: from.id, direction: OppositeDirection[direction], isHidden: false, isLocked: false }
            : null,
        )
      }
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
      <TextBox label="Description" value={description} rows={2} onChange={setDescription} />
      <Segmented label="Who can enter" value={accessType} options={AccessOptions} onChange={setAccessType} />
      {spot.from && <Check label={`Exit both ways (to and from ${spot.from.room.name})`} checked={bothWays} onChange={setBothWays} />}
      {action.error && (
        <StatusBanner tone="danger" role="alert">
          {action.error}
        </StatusBanner>
      )}
      <div className="forge__actions">
        <Button type="submit" intent="primary" busy={action.busy} disabled={!name.trim() || !description.trim()}>
          Create room
        </Button>
        <Button onClick={onCancel} disabled={action.busy}>
          Cancel
        </Button>
      </div>
    </form>
  )
}
