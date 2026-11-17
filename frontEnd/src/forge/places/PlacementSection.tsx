import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { duplicateRoom, moveRooms, publishRooms, setRoomsDistrict, unpublishRooms, type WorldRoom } from '../api.ts'
import { Chip, NumberInput, Section, Select } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'

const Far = 1_000_000

/**
 * WF-60 and WF-46: whether the room is in play, and where it is. Publishing
 * puts the room, the exits into it and what is in it in play; unpublishing
 * takes them out and puts anyone inside in a live room an exit leads to.
 * Moving keeps every exit as it was authored.
 */
export function PlacementSection({ room }: { room: WorldRoom }) {
  const { world, refreshWorld, openMap } = useForgeData()
  const [moving, setMoving] = useState<'move' | 'copy' | false>(false)
  const [target, setTarget] = useState({ mapId: room.mapId, x: room.mapX, y: room.mapY, layer: room.mapLayer })
  const [notice, setNotice] = useState<string | null>(null)
  const action = useAction()

  async function publish() {
    setNotice(null)
    const done = await action.run(async () => {
      await publishRooms({ roomIds: [room.id] })
      await refreshWorld()
      return true
    })
    if (done) setNotice(`${room.name} is in play.`)
  }

  async function unpublish() {
    if (!window.confirm(`Take ${room.name} out of play? Anyone inside is moved to a live room next door.`)) return
    setNotice(null)
    const result = await action.run(async () => {
      const done = await unpublishRooms({ roomIds: [room.id] })
      await refreshWorld()
      return done
    })
    if (result) {
      setNotice(
        result.charactersMoved > 0
          ? `${room.name} is a draft again. ${result.charactersMoved === 1 ? 'One character was' : `${result.charactersMoved} characters were`} moved out.`
          : `${room.name} is a draft again.`,
      )
    }
  }

  // WF-73: the room's district, if any.
  async function setDistrict(districtId: string) {
    setNotice(null)
    const done = await action.run(async () => {
      await setRoomsDistrict([room.id], districtId || null)
      await refreshWorld()
      return true
    })
    if (done) setNotice(districtId ? `${room.name} is in ${world.districts?.find((district) => district.id === districtId)?.name}.` : `${room.name} is in no district.`)
  }

  // WF-44: a draft copy with its NPCs and inspectables, on the cell chosen.
  async function duplicate() {
    setNotice(null)
    const copy = await action.run(async () => {
      const made = await duplicateRoom(room.id, { mapId: target.mapId, x: target.x, y: target.y, layer: target.layer })
      if (target.mapId !== room.mapId) openMap(target.mapId)
      else await refreshWorld()
      return made
    })
    if (copy) {
      setMoving(false)
      setNotice(`${copy.name} built at ${copy.mapX}, ${copy.mapY}, as a draft, with what was in ${room.name}.`)
    }
  }

  async function move() {
    if (moving === 'copy') return duplicate()
    setNotice(null)
    const done = await action.run(async () => {
      await moveRooms({
        roomIds: [room.id],
        toMapId: target.mapId === room.mapId ? null : target.mapId,
        deltaX: target.x - room.mapX,
        deltaY: target.y - room.mapY,
        deltaLayer: target.layer - room.mapLayer,
      })
      if (target.mapId !== room.mapId) openMap(target.mapId)
      else await refreshWorld()
      return true
    })
    if (done) {
      setMoving(false)
      setNotice(`${room.name} moved. Its exits are as they were.`)
    }
  }

  const unchanged = target.mapId === room.mapId && target.x === room.mapX && target.y === room.mapY && target.layer === room.mapLayer

  return (
    <Section
      title="In play"
      actions={<Chip tone={room.isLive ? 'live' : 'draft'}>{room.isLive ? 'Live' : 'Draft'}</Chip>}
    >
      <div className="forge__stack">
        <p className="forge__hint">
          {room.isLive
            ? 'Players can walk in. Unpublishing takes the room, the exits into it and what is in it out of play.'
            : 'A draft: players cannot reach it, and a job that uses it cannot go live.'}{' '}
          At {room.mapX}, {room.mapY} on layer {room.mapLayer}, {world.map.name}.
        </p>
        <div className="forge__actions">
          {room.isLive ? (
            <Button onClick={() => void unpublish()} busy={action.busy}>
              Unpublish room
            </Button>
          ) : (
            <Button intent="primary" onClick={() => void publish()} busy={action.busy}>
              Publish room
            </Button>
          )}
          <Button
            aria-expanded={moving === 'move'}
            onClick={() => {
              setTarget({ mapId: room.mapId, x: room.mapX, y: room.mapY, layer: room.mapLayer })
              setMoving((open) => (open === 'move' ? false : 'move'))
            }}
          >
            Move room
          </Button>
          <Button
            aria-expanded={moving === 'copy'}
            onClick={() => {
              setTarget({ mapId: room.mapId, x: room.mapX + 1, y: room.mapY, layer: room.mapLayer })
              setMoving((open) => (open === 'copy' ? false : 'copy'))
            }}
          >
            Duplicate
          </Button>
        </div>
        {(world.districts ?? []).length > 0 && (
          <Select
            label="District"
            value={room.districtId ?? ''}
            placeholder="No district"
            options={(world.districts ?? []).map((district) => ({ id: district.id, displayName: district.name }))}
            onChange={(districtId) => void setDistrict(districtId)}
          />
        )}
        {moving && (
          <form
            className="forge__stack"
            aria-label={moving === 'copy' ? 'Duplicate room' : 'Move room'}
            onSubmit={(event) => {
              event.preventDefault()
              void move()
            }}
          >
            <Select
              label="To map"
              value={target.mapId}
              options={world.maps.map((map) => ({ id: map.id, displayName: map.name }))}
              onChange={(mapId) => setTarget((current) => ({ ...current, mapId }))}
            />
            <div className="forge__mapbar">
              <NumberInput label="X" value={target.x} min={-Far} max={Far} onChange={(x) => setTarget((current) => ({ ...current, x: x ?? current.x }))} />
              <NumberInput label="Y" value={target.y} min={-Far} max={Far} onChange={(y) => setTarget((current) => ({ ...current, y: y ?? current.y }))} />
              <NumberInput label="Layer" value={target.layer} min={-Far} max={Far} onChange={(layer) => setTarget((current) => ({ ...current, layer: layer ?? current.layer }))} />
            </div>
            <p className="forge__hint">
              {moving === 'copy'
                ? 'The cell must be free. The copy takes the NPCs (without a conversation) and inspectables; not exits, events or job items.'
                : 'The cell must be free. Exits stay as they are, even to rooms that are no longer next door.'}
            </p>
            <div className="forge__actions">
              <Button type="submit" intent="primary" disabled={moving === 'move' && unchanged} busy={action.busy}>
                {moving === 'copy' ? 'Duplicate here' : 'Move here'}
              </Button>
              <Button onClick={() => setMoving(false)} disabled={action.busy}>
                Cancel
              </Button>
            </div>
          </form>
        )}
        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
        {notice && !action.error && <StatusBanner tone="success">{notice}</StatusBanner>}
      </div>
    </Section>
  )
}
