import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { deleteRoom, getRoomDeletion, moveRooms, publishRooms, setRoomsDistrict, unpublishRooms, type Guid } from '../api.ts'
import { NumberInput, Select } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'

const Far = 1_000_000
const NoDistrict = 'none'

/**
 * WF-72: rooms picked on the map, and one action for all of them: publish,
 * unpublish, move by an offset, set the district, or delete.
 */
export function SelectionBar({ selected, onClear }: { selected: ReadonlySet<Guid>; onClear: () => void }) {
  const { world, refreshWorld } = useForgeData()
  const [offset, setOffset] = useState({ x: 0, y: 0, layer: 0 })
  const [district, setDistrict] = useState('')
  const [notice, setNotice] = useState<string | null>(null)
  const action = useAction()
  const roomIds = [...selected]
  const count = roomIds.length
  const rooms = (n: number) => (n === 1 ? '1 room' : `${n} rooms`)

  async function run(work: () => Promise<string>) {
    setNotice(null)
    const done = await action.run(async () => {
      const message = await work()
      await refreshWorld()
      return message
    })
    if (done) setNotice(done)
  }

  async function removeAll() {
    if (!window.confirm(`Delete ${rooms(count)}, with their exits and what is in them? This cannot be undone.`)) return
    await run(async () => {
      // Each room is checked as one delete would be; any that cannot go is named.
      const refused: string[] = []
      let deleted = 0
      for (const roomId of roomIds) {
        const name = world.rooms.find((room) => room.id === roomId)?.name ?? 'A room'
        const check = await getRoomDeletion(roomId)
        if (!check.canDelete || check.charactersPresent > 0) {
          refused.push(`${name}: ${check.reason ?? 'characters are standing in it; delete it on its own to move them'}`)
          continue
        }
        const result = await deleteRoom(roomId)
        if (result.canDelete) deleted++
        else refused.push(`${name}: ${result.reason ?? 'refused'}`)
      }
      onClear()
      if (refused.length > 0) throw new Error(`${rooms(deleted)} deleted. Not deleted — ${refused.join('; ')}`)
      return `${rooms(deleted)} deleted.`
    })
  }

  return (
    <div className="forge__stack forge__selection" aria-label="Selected rooms" role="region">
      <p className="forge__hint">
        {count === 0 ? 'Choose rooms on the map to select them.' : `${rooms(count)} selected.`}
      </p>
      {count > 0 && (
        <>
          <div className="forge__actions">
            <Button
              intent="primary"
              busy={action.busy}
              onClick={() =>
                void run(async () => `${rooms((await publishRooms({ roomIds })).roomIds.length)} put in play.`)
              }
            >
              Publish
            </Button>
            <Button
              busy={action.busy}
              onClick={() =>
                void run(async () => {
                  const result = await unpublishRooms({ roomIds })
                  return `${rooms(result.roomIds.length)} taken out of play${result.charactersMoved ? `; ${result.charactersMoved} characters moved` : ''}.`
                })
              }
            >
              Unpublish
            </Button>
            <Button busy={action.busy} onClick={() => void removeAll()}>
              Delete
            </Button>
            <Button onClick={onClear} disabled={action.busy}>
              Clear selection
            </Button>
          </div>
          <div className="forge__mapbar">
            <NumberInput label="Move east by" value={offset.x} min={-Far} max={Far} onChange={(x) => setOffset((current) => ({ ...current, x: x ?? 0 }))} />
            <NumberInput label="Move north by" value={offset.y} min={-Far} max={Far} onChange={(y) => setOffset((current) => ({ ...current, y: y ?? 0 }))} />
            <NumberInput label="Move up by" value={offset.layer} min={-Far} max={Far} onChange={(layer) => setOffset((current) => ({ ...current, layer: layer ?? 0 }))} />
            <Button
              busy={action.busy}
              disabled={offset.x === 0 && offset.y === 0 && offset.layer === 0}
              onClick={() =>
                void run(async () => {
                  await moveRooms({ roomIds, deltaX: offset.x, deltaY: offset.y, deltaLayer: offset.layer })
                  return `${rooms(count)} moved. Their exits are as they were.`
                })
              }
            >
              Move
            </Button>
          </div>
          <div className="forge__mapbar">
            <Select
              label="District"
              value={district}
              placeholder="Choose a district"
              options={[
                { id: NoDistrict, displayName: 'No district' },
                ...(world.districts ?? []).map((candidate) => ({ id: candidate.id, displayName: candidate.name })),
              ]}
              onChange={setDistrict}
            />
            <Button
              busy={action.busy}
              disabled={district === ''}
              onClick={() =>
                void run(async () => {
                  const result = await setRoomsDistrict(roomIds, district === NoDistrict ? null : district)
                  return `${rooms(result.roomIds.length)} changed district.`
                })
              }
            >
              Set district
            </Button>
          </div>
        </>
      )}
      {action.error && (
        <StatusBanner tone="danger" role="alert">
          {action.error}
        </StatusBanner>
      )}
      {notice && !action.error && <StatusBanner tone="success">{notice}</StatusBanner>}
    </div>
  )
}
